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
using System.Web;
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
        private const int MaxFilesPerSecs = 9; // Dropbox rate-limit at 600/min or 10/s

        private readonly ITracer _tracer;
        private readonly IDeploymentStatusManager _status;
        private readonly IDeploymentSettingsManager _settings;
        private readonly IEnvironment _environment;
        private readonly TimeSpan _timeout;

        private ILogger _logger;

        // stats
        private int _totals;
        private int _successCount;
        private int _fileCount;
        private int _failedCount;
        private int _retriedCount;

        public DropboxHelper(ITracer tracer,
                             IDeploymentStatusManager status,
                             IDeploymentSettingsManager settings,
                             IEnvironment environment)
        {
            _tracer = tracer;
            _status = status;
            _settings = settings;
            _environment = environment;
            _timeout = settings.GetCommandIdleTimeout();
        }

        internal async Task<ChangeSet> Sync(DropboxHandler.DropboxInfo deploymentInfo, string branch, ILogger logger, IRepository repository)
        {
            DropboxDeployInfo info = deploymentInfo.DeployInfo;

            _logger = logger;

            _totals = 0;
            _successCount = 0;
            _fileCount = 0;
            _failedCount = 0;
            _retriedCount = 0;

            if (_settings.GetValue(CursorKey) != info.OldCursor)
            {
                throw new InvalidOperationException(Resources.Error_MismatchDropboxCursor);
            }

            if (!repository.IsEmpty())
            {
                // git checkout --force <branch>
                repository.ClearLock();
                repository.Update(branch);
            }

            ChangeSet changeSet;
            string message = null;
            try
            {
                using (_tracer.Step("Synch with Dropbox"))
                {
                    // Sync dropbox => repository directory
                    await ApplyChanges(deploymentInfo);
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

                IDeploymentStatusFile statusFile = _status.Open(deploymentInfo.TargetChangeset.Id);
                statusFile.UpdateMessage(message);
                statusFile.UpdateProgress(String.Format(CultureInfo.CurrentCulture, Resources.Dropbox_Committing, _successCount));

                // Commit anyway even partial change
                changeSet = repository.Commit(message, String.Format("{0} <{1}>", info.UserName, info.Email));
            }

            // Save new dropboc cursor
            LogInfo("Update dropbox cursor");
            _settings.SetValue(CursorKey, info.NewCursor);

            return changeSet;
        }

        private void UpdateStatusFile(object state)
        {
            string changeset = (string)state;
            IDeploymentStatusFile statusFile =_status.Open(changeset);
            statusFile.UpdateProgress(String.Format(CultureInfo.CurrentUICulture,
                                        _failedCount == 0 ? Resources.Dropbox_SynchronizingProgress : Resources.Dropbox_SynchronizingProgressWithFailure,
                                        ((_successCount + _failedCount) * 100) / _totals,
                                        _totals,
                                        _failedCount));
        }

        private async Task ApplyChanges(DropboxHandler.DropboxInfo deploymentInfo)
        {
            DropboxDeployInfo info = deploymentInfo.DeployInfo;
            _totals = info.Deltas.Count;

            using (new Timer(UpdateStatusFile, state: deploymentInfo.TargetChangeset.Id, dueTime: TimeSpan.FromSeconds(5), period: TimeSpan.FromSeconds(5)))
            {
                await ApplyChangesCore(info);
            }
        }

        private Task ApplyChangesCore(DropboxDeployInfo info)
        {
            List<Task> tasks = new List<Task>();
            string parent = info.Path.TrimEnd('/') + '/';
            DateTime updateMessageTime = DateTime.UtcNow;
            int totals = info.Deltas.Count();
            var sem = new SemaphoreSlim(MaxConcurrentRequests, MaxConcurrentRequests);
            var rateLimiter = new RateLimiter(MaxFilesPerSecs * 10, TimeSpan.FromSeconds(10));

            foreach (DropboxDeltaInfo delta in info.Deltas)
            {
                if (!delta.Path.StartsWith(parent, StringComparison.OrdinalIgnoreCase))
                {
                    Interlocked.Increment(ref _successCount);
                    continue;
                }

                // Ignore .git and .hg files and folders
                string pathWithSlash = delta.Path + "/";
                if (pathWithSlash.IndexOf("/.git/", StringComparison.OrdinalIgnoreCase) != -1 ||
                    pathWithSlash.IndexOf("/.hg/", StringComparison.OrdinalIgnoreCase) != -1)
                {
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
                    DateTime modified;
                    if (DateTime.TryParse(delta.Modified, out modified))
                    {
                        modified = modified.ToUniversalTime();
                    }
                    else
                    {
                        modified = DateTime.UtcNow;
                    }

                    if (!IsFileChanged(parent, path, modified))
                    {
                        LogInfo("file unchanged {0}", path);
                        Interlocked.Increment(ref _successCount);
                        continue;
                    }

                    try
                    {
                        Task task = Task.Run(async () =>
                        {
                            try
                            {
                                sem.Wait();
                                rateLimiter.Throtte();
                                await ProcessFileAsync(info, delta, parent, path, modified);
                            }
                            finally
                            {
                                sem.Release();
                            }
                        });
                        
                        tasks.Add(task);
                    }
                    catch (Exception ex)
                    {
                        LogError("{0}", ex);
                        throw;
                    }
                }
            }

            return Task.WhenAll(tasks);
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

        private async Task SafeWriteFile(string parent, string path, DateTime lastModifiedUtc, Stream stream)
        {
            var fullPath = GetRepositoryPath(parent, path);
            if (Directory.Exists(fullPath))
            {
                Directory.Delete(fullPath, recursive: true);
                LogInfo("rmdir /s " + path);
            }

            using (FileStream fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write))
            {
                await stream.CopyToAsync(fs);
                LogInfo("write {0,6} bytes to {1}", fs.Length, path);
            }

            File.SetLastWriteTimeUtc(fullPath, lastModifiedUtc);
        }

        private string GetRepositoryPath(string parent, string path)
        {
            string relativePath = path.Substring(parent.Length).Replace('/', '\\');
            return Path.Combine(_environment.RepositoryPath, relativePath);
        }

        private async Task ProcessFileAsync(DropboxDeployInfo info, DropboxDeltaInfo delta, string parent, string path, DateTime lastModifiedUtc)
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

            int retries = MaxRetries;

            while (retries >= 0)
            {
                retries--;

                using (var client = new HttpClient { BaseAddress = new Uri(DropboxApiContentUri), Timeout = _timeout })
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("OAuth", sb.ToString());
                    try
                    {
                        string requestPath = SandboxFilePath + DropboxPathEncode(delta.Path.ToLowerInvariant());
                        using (HttpResponseMessage response = await client.GetAsync(requestPath))
                        {
                            using (Stream stream = await response.EnsureSuccessStatusCode().Content.ReadAsStreamAsync())
                            {
                                await SafeWriteFile(parent, path, lastModifiedUtc, stream);
                            }
                        }
                        if (retries < MaxRetries - 1)
                        {
                            Interlocked.Increment(ref _retriedCount);
                        }
                        Interlocked.Increment(ref _successCount);

                        break;
                    }
                    catch (Exception ex)
                    {
                        if (retries <= 0)
                        {
                            Interlocked.Increment(ref _failedCount);
                            LogError("Get({0}) '{1}'failed with {2}", MaxRetries - retries - 1, SandboxFilePath + delta.Path, ex.Message);
                            break;
                        }
                        else
                        {
                            // First retry is 1s, second retry is 20s assuming rate-limit
                            Thread.Sleep(retries == 1 ? TimeSpan.FromSeconds(20) : TimeSpan.FromSeconds(1));
                            continue;
                        }
                    }
                    finally
                    {
                        Interlocked.Increment(ref _fileCount);
                    }
                }
            }
        }

        // Ported from https://github.com/SpringSource/spring-net-social-dropbox/blob/master/src/Spring.Social.Dropbox/Social/Dropbox/Api/Impl/DropboxTemplate.cs#L1713
        private static string DropboxPathEncode(string path)
        {
            const string DropboxPathUnreservedChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_./";

            StringBuilder result = new StringBuilder();
            foreach (char symbol in path)
            {
                if (DropboxPathUnreservedChars.IndexOf(symbol) != -1)
                {
                    result.Append(symbol);
                }
                else
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(new char[] { symbol });
                    foreach (byte b in bytes)
                    {
                        result.AppendFormat("%{0:X2}", b);
                    }
                }
            }

            return result.ToString();
        }

        private void LogInfo(string value, params object[] args)
        {
            string message = String.Format(CultureInfo.CurrentCulture, value, args);
            lock (_tracer)
            {
                _tracer.Trace(message);
            }
        }

        private void LogError(string value, params object[] args)
        {
            string message = String.Format(CultureInfo.CurrentCulture, value, args);
            lock (_tracer)
            {
                _logger.Log(message, LogEntryType.Error);
                _tracer.TraceError(message);
            }
        }

        public class RateLimiter
        {
            private readonly object _thisLock = new object();
            private readonly int _limit;
            private readonly TimeSpan _interval;

            private int _current = 0;
            private DateTime _last = DateTime.UtcNow;

            public RateLimiter(int limit, TimeSpan interval)
            {
                _limit = limit;
                _interval = interval;
            }

            public void Throtte()
            {
                lock (_thisLock)
                {
                    _current++;
                    if (_current > _limit)
                    {
                        DateTime now = DateTime.UtcNow;
                        TimeSpan ts = now - _last;
                        if (ts < _interval)
                        {
                            Thread.Sleep(_interval - ts);
                            _last = DateTime.UtcNow;
                        }
                        else
                        {
                            _last = now;
                        }

                        _current = 0;
                    }
                }
            }
        }
    }
}
