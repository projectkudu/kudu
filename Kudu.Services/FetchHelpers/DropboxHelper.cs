using System;
using System.Collections.Generic;
using System.IO;
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
using Kudu.Core.SourceControl;

namespace Kudu.Services
{
    public class DropboxHelper
    {
        public const string Dropbox = "dropbox";
        public const string CursorKey = "dropbox_cursor";
        public const string UserNameKey = "dropbox_username";
        public const string EmailKey = "dropbox_email";

        private const string DropboxApiContentUri = "https://api-content.dropbox.com/";
        private const string SandboxFilePath = "1/files/sandbox";
        private const int MaxConcurrentRequests = 5;

        private readonly ITracer _tracer;
        private readonly IServerRepository _repository;
        private readonly IDeploymentSettingsManager _settings;
        private readonly IEnvironment _environment;
        private readonly TimeSpan _timeout;

        public DropboxHelper(ITracer tracer, 
                             IServerRepository repository, 
                             IDeploymentSettingsManager settings, 
                             IEnvironment environment)
        {
            _tracer = tracer;
            _repository = repository;
            _settings = settings;
            _environment = environment;
            _timeout = settings.GetCommandIdleTimeout();
        }

        public ChangeSet Sync(DropboxDeployInfo info, string branch)
        {
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
            string prefix = "Partially"; 
            try
            {
                using (_tracer.Step("Synch with Dropbox"))
                {
                    // Sync dropbox => repository directory
                    ApplyChanges(info);
                }

                prefix = "Successfully";
            }
            finally
            {
                // Commit anyway even partial change
                changeSet = _repository.Commit(prefix + " sync with dropbox at " + DateTime.UtcNow.ToString("g"), GetAuthor());
            }

            // Save new dropboc cursor
            _tracer.Trace("Update dropbox cursor");
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

        private string GetAuthor()
        {
            string userName = _settings.GetValue(UserNameKey);
            string email = _settings.GetValue(EmailKey);
            if (!String.IsNullOrEmpty(userName) && !String.IsNullOrEmpty(email))
            {
                return String.Format("{0} <{1}>", userName, email);
            }

            return null;
        }

        private void ApplyChanges(DropboxDeployInfo info)
        {
            Semaphore sem = new Semaphore(MaxConcurrentRequests, MaxConcurrentRequests);
            List<Task> tasks = new List<Task>();
            string parent = info.Path.TrimEnd('/') + '/'; 

            foreach (DropboxDeltaInfo delta in info.Deltas)
            {
                if (!delta.Path.StartsWith(parent, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var path = delta.Path;
                if (delta.IsDeleted)
                {
                    SafeDelete(parent, path);
                }
                else if (delta.IsDirectory)
                {
                    SafeCreateDir(parent, path);
                }
                else
                {
                    DateTime modified = DateTime.Parse(delta.Modified).ToUniversalTime();
                    if (!IsFileChanged(parent, path, modified))
                    {
                        _tracer.Trace("file unchanged {0}", path);
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
                                using (Stream stream = t.Result)
                                {
                                    SafeWriteFile(parent, path, stream, modified);
                                }
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        sem.Release();
                        _tracer.TraceError(ex);
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
                _tracer.Trace("del " + path);
            }
            else if (Directory.Exists(fullPath))
            {
                Directory.Delete(fullPath, recursive: true);
                _tracer.Trace("rmdir /s " + path);
            }
        }

        private void SafeCreateDir(string parent, string path)
        {
            var fullPath = GetRepositoryPath(parent, path);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                _tracer.Trace("del " + path);
            }

            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
                _tracer.Trace("mkdir " + path);
            }
        }

        private void SafeWriteFile(string parent, string path, Stream stream, DateTime modified)
        {
            var fullPath = GetRepositoryPath(parent, path);
            if (Directory.Exists(fullPath))
            {
                Directory.Delete(fullPath, true);
                _tracer.Trace("rmdir /s " + path);
            }

            using (FileStream fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write))
            {
                stream.CopyTo(fs);
                _tracer.Trace("write {0,6} bytes to {1}", fs.Length, path);
            }

            File.SetLastWriteTimeUtc(fullPath, modified);
        }

        private string GetRepositoryPath(string parent, string path)
        {
            string relativePath = path.Substring(parent.Length).Replace('/', '\\');
            return Path.Combine(_environment.RepositoryPath, relativePath);
        }

        private Task<Stream> GetFileAsync(DropboxDeployInfo info, DropboxDeltaInfo delta)
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
            TaskCompletionSource<Task<Stream>> tcs = new TaskCompletionSource<Task<Stream>>();
            client.GetAsync(SandboxFilePath + delta.Path).ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    _tracer.TraceError(t.Exception);
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
                        tcs.TrySetResult(t.Result.EnsureSuccessStatusCode().Content.ReadAsStreamAsync());
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                }
            });

            return tcs.Task.FastUnwrap();
        }
    }
}
