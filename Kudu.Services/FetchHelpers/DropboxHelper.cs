using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Kudu.Common;
using Kudu.Contracts.Dropbox;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Core.Deployment;
using Kudu.Core.SourceControl;
using Kudu.Services.ServiceHookHandlers;

namespace Kudu.Services
{
    public class DropboxHelper
    {
        public const string Dropbox = "Dropbox";
        public const string CursorKey = "dropbox_cursor";

        private const string DropboxApiContentUri = "https://api-content.dropbox.com/";
        private const string SandboxFilePath = "1/files/sandbox";
        private const int MaxConcurrentRequests = 5;
        private const int MaxRetries = 2;

        private readonly ITracer _tracer;
        private readonly IServerRepository _repository;
        private readonly IDeploymentManager _manager;
        private readonly IDeploymentSettingsManager _settings;
        private readonly IEnvironment _environment;
        private readonly TimeSpan _timeout;

        private ILogger _logger;

        // stats
        private int _successCount;
        private int _fileCount;
        private int _failedCount;
        private int _retriedCount;

        public DropboxHelper(ITracer tracer,
                             IServerRepository repository,
                             IDeploymentManager manager,
                             IDeploymentSettingsManager settings,
                             IEnvironment environment)
        {
            _tracer = tracer;
            _repository = repository;
            _manager = manager;
            _settings = settings;
            _environment = environment;
            _timeout = settings.GetCommandIdleTimeout();
        }

        internal ChangeSet Sync(DropboxHandler.DropboxInfo deploymentInfo, string branch, ILogger logger)
        {
            DropboxDeployInfo info = deploymentInfo.DeployInfo;

            _logger = logger;

            _successCount = 0;
            _fileCount = 0;
            _failedCount = 0;
            _retriedCount = 0;

            if (_settings.GetValue(CursorKey) != info.OldCursor)
            {
                throw new InvalidOperationException(Resources.Error_MismatchDropboxCursor);
            }

            if (!IsEmptyRepo())
            {
                // git checkout --force <branch>
                _repository.Update(branch);
            }

            ChangeSet changeSet;
            string message = null;
            try
            {
                using (_tracer.Step("Synch with Dropbox"))
                {
                    // Sync dropbox => repository directory
                    ApplyChanges(deploymentInfo);
                }

                message = String.Format(CultureInfo.CurrentCulture,
                            Resources.Dropbox_Synchronized,
                            deploymentInfo.DeployInfo.Deltas.Count());
            }
            catch (Exception)
            {
                message = String.Format(CultureInfo.CurrentCulture,
                            Resources.Dropbox_SynchronizedWithFailure,
                            _successCount,
                            deploymentInfo.DeployInfo.Deltas.Count(),
                            _failedCount);

                throw;
            }
            finally
            {
                _logger.Log(message);

                _logger.Log(String.Format("{0} downloaded files, {1} successful retries.", _fileCount, _retriedCount));

                // Commit anyway even partial change
                changeSet = _repository.Commit(message, String.Format("{0} <{1}>", info.UserName, info.Email));

                _manager.UpdateMessage(deploymentInfo.TargetChangeset.Id, message);
            }

            // Save new dropboc cursor
            LogInfo("Update dropbox cursor");
            _settings.SetValue(CursorKey, info.NewCursor);

            return changeSet;
        }

        private bool IsEmptyRepo()
        {
            try
            {
                return string.IsNullOrEmpty(_repository.CurrentId);
            }
            catch (Exception)
            {
                return true;
            }
        }

        private void ApplyChanges(DropboxHandler.DropboxInfo deploymentInfo)
        {
            DropboxDeployInfo info = deploymentInfo.DeployInfo;
            Semaphore sem = new Semaphore(MaxConcurrentRequests, MaxConcurrentRequests);
            List<Task> tasks = new List<Task>();
            string parent = info.Path.TrimEnd('/') + '/';
            DateTime updateMessageTime = DateTime.Now;
            int totals = info.Deltas.Count();

            foreach (DropboxDeltaInfo delta in info.Deltas)
            {
                if (!delta.Path.StartsWith(parent, StringComparison.OrdinalIgnoreCase))
                {
                    Interlocked.Increment(ref _successCount);
                    continue;
                }

                // Ignore .git files and folders
                if (delta.Path.StartsWith(parent + ".git", StringComparison.OrdinalIgnoreCase))
                {
                    Interlocked.Increment(ref _successCount);
                    continue;
                }

                var path = delta.Path;
                if (delta.IsDeleted)
                {
                    SafeDelete(parent, path);
                    Interlocked.Increment(ref _successCount);
                }
                else if (delta.IsDirectory)
                {
                    SafeCreateDir(parent, path);
                    Interlocked.Increment(ref _successCount);
                }
                else
                {
                    DateTime modified = DateTime.Parse(delta.Modified).ToUniversalTime();
                    if (!IsFileChanged(parent, path, modified))
                    {
                        LogInfo("file unchanged {0}", path);
                        Interlocked.Increment(ref _successCount);
                        continue;
                    }

                    // throttle concurrent get file dropbox
                    sem.WaitOne();
                    Task task;
                    try
                    {
                        // Using ContinueWith instead of Then to avoid SyncContext deadlock in 4.5
                        task = GetFileAsync(info, delta).ContinueWith(t =>
                        {
                            sem.Release();
                            if (!t.IsFaulted && !t.IsCanceled)
                            {
                                using (StreamInfo streamInfo = t.Result)
                                {
                                    SafeWriteFile(parent, path, streamInfo.Stream, modified);
                                }

                                Interlocked.Increment(ref _successCount);
                                Interlocked.Increment(ref _fileCount);
                            }
                            else
                            {
                                Interlocked.Increment(ref _failedCount);
                            }

                            if (DateTime.Now.Subtract(updateMessageTime) > TimeSpan.FromSeconds(5))
                            {
                                lock (_manager)
                                {
                                    _manager.UpdateMessage(deploymentInfo.TargetChangeset.Id,
                                        String.Format(CultureInfo.CurrentUICulture, 
                                            _failedCount == 0 ? Resources.Dropbox_SynchronizingProgress : Resources.Dropbox_SynchronizingProgressWithFailure,
                                            ((_successCount + _failedCount) * 100) / totals,
                                            totals,
                                            _failedCount));
                                }

                                updateMessageTime = DateTime.Now;
                            }

                            return t;
                        }).FastUnwrap();
                    }
                    catch (Exception ex)
                    {
                        sem.Release();
                        LogError("{0}", ex);
                        Task.WaitAll(tasks.ToArray(), _timeout);
                        throw;
                    }

                    tasks.Add(task);
                }
            }

            if (!Task.WaitAll(tasks.ToArray(), _timeout))
            {
                throw new TimeoutException(RS.Format(Resources.Error_SyncDropboxTimeout, (int)_timeout.TotalSeconds));
            }
        }

        private bool IsFileChanged(string parent, string path, DateTime modified)
        {
            var file = new System.IO.FileInfo(GetRepositoryPath(parent, path));
            return !file.Exists || file.LastWriteTimeUtc != modified;
        }

        private void SafeDelete(string parent, string path)
        {
            var fullPath = GetRepositoryPath(parent, path);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                LogInfo("del " + path);
            }
            else if (Directory.Exists(fullPath))
            {
                Directory.Delete(fullPath, recursive: true);
                LogInfo("rmdir /s " + path);
            }
        }

        private void SafeCreateDir(string parent, string path)
        {
            var fullPath = GetRepositoryPath(parent, path);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                LogInfo("del " + path);
            }

            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
                LogInfo("mkdir " + path);
            }
        }

        private void SafeWriteFile(string parent, string path, Stream stream, DateTime modified)
        {
            var fullPath = GetRepositoryPath(parent, path);
            if (Directory.Exists(fullPath))
            {
                Directory.Delete(fullPath, true);
                LogInfo("rmdir /s " + path);
            }

            using (FileStream fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write))
            {
                stream.CopyTo(fs);
                LogInfo("write {0,6} bytes to {1}", fs.Length, path);
            }

            File.SetLastWriteTimeUtc(fullPath, modified);
        }

        private string GetRepositoryPath(string parent, string path)
        {
            string relativePath = path.Substring(parent.Length).Replace('/', '\\');
            return Path.Combine(_environment.RepositoryPath, relativePath);
        }

        private Task<StreamInfo> GetFileAsync(DropboxDeployInfo info, DropboxDeltaInfo delta, int retries = MaxRetries)
        {
            var parameters = new Dictionary<string, string>
            {
                { "oauth_consumer_key", info.ConsumerKey },
                { "oauth_signature_method", info.SignatureMethod },
                { "oauth_timestamp", info.TimeStamp },
                { "oauth_nonce", delta.Nonce },
                { "oauth_version", info.OAuthVersion },
                { "oauth_token", info.Token },
                { "oauth_signature", delta.Signature }
            };

            var sb = new StringBuilder();
            foreach (var key in parameters.Keys)
            {
                if (sb.Length != 0)
                {
                    sb.Append(',');
                }
                sb.AppendFormat("{0}=\"{1}\"", key, parameters[key]);
            }

            var client = new HttpClient();
            client.BaseAddress = new Uri(DropboxApiContentUri);
            client.Timeout = _timeout;
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("OAuth", sb.ToString());

            // Using ContinueWith instead of Then to avoid SyncContext deadlock in 4.5
            var tcs = new TaskCompletionSource<Task<StreamInfo>>();
            client.GetAsync(SandboxFilePath + delta.Path.ToLower()).ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    using (client)
                    {
                        if (retries <= 0)
                        {
                            LogError("Get '" + SandboxFilePath + delta.Path + "' failed with " + t.Exception);
                            tcs.TrySetException(t.Exception.InnerExceptions);
                        }
                        else
                        {
                            tcs.TrySetResult(RetryGetFileAsync(info, delta, retries));
                        }
                    }
                }
                else if (t.IsCanceled)
                {
                    using (client)
                    {
                        tcs.TrySetCanceled();
                    }
                }
                else
                {
                    try
                    {
                        HttpResponseMessage response = t.Result;
                        if ((int)response.StatusCode < 500 || retries <= 0)
                        {
                            response.EnsureSuccessStatusCode();
                            tcs.TrySetResult(GetStreamInfo(client, response, delta.Path));
                            if (retries < MaxRetries)
                            {
                                // Success due to retried
                                Interlocked.Increment(ref _retriedCount);
                            }
                        }
                        else
                        {
                            using (client)
                            {
                                using (response)
                                {
                                    tcs.TrySetResult(RetryGetFileAsync(info, delta, retries));
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError("Get '" + SandboxFilePath + delta.Path + "' failed with " + ex);
                        tcs.TrySetException(ex);
                    }
                }
            });

            return tcs.Task.FastUnwrap();
        }

        private Task<StreamInfo> RetryGetFileAsync(DropboxDeployInfo info, DropboxDeltaInfo delta, int retries)
        {
            // First retry is 1s, second retry is 20s assuming rate-limit
            Thread.Sleep(retries == 1 ? 20000 : 1000);

            return GetFileAsync(info, delta, retries - 1);
        }

        private void LogInfo(string value, params object[] args)
        {
            string message = String.Format(CultureInfo.CurrentCulture, value, args);
            _tracer.Trace(message);
        }

        private void LogError(string value, params object[] args)
        {
            string message = String.Format(CultureInfo.CurrentCulture, value, args);
            _logger.Log(message, LogEntryType.Error);
            _tracer.TraceError(message);
        }

        private Task<StreamInfo> GetStreamInfo(HttpClient client, HttpResponseMessage response, string path)
        {
            var tcs = new TaskCompletionSource<StreamInfo>();

            response.Content.ReadAsStreamAsync().ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    LogError("Get '" + SandboxFilePath + path + "' failed with " + t.Exception);
                    tcs.TrySetException(t.Exception.InnerExceptions);
                }
                else if (t.IsCanceled)
                {
                    tcs.TrySetCanceled();
                }
                else
                {
                    try
                    {
                        tcs.TrySetResult(new StreamInfo(client, response, t.Result));
                    }
                    catch (Exception ex)
                    {
                        LogError("Get '" + SandboxFilePath + path + "' failed with " + ex);
                        tcs.TrySetException(ex);
                    }
                }
            });

            return tcs.Task;
        }

        class StreamInfo : IDisposable
        {
            private readonly HttpClient _client;
            private readonly HttpResponseMessage _response;
            private readonly Stream _stream;

            public StreamInfo(HttpClient client, HttpResponseMessage response, Stream stream)
            {
                _client = client;
                _response = response;
                _stream = stream;
            }

            public Stream Stream
            {
                get { return this._stream; }
            }

            public void Dispose()
            {
                // optimize codes for success
                // we don't do nested try/finally to handle failure dispose
                // we let GC take care of that
                _stream.Dispose();
                _response.Dispose();
                _client.Dispose();
            }
        }
    }
}
