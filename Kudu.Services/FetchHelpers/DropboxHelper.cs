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
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Core.Deployment;
using Kudu.Core.Infrastructure;
using Kudu.Core.SourceControl;
using Kudu.Core.Tracing;
using Kudu.Services.FetchHelpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Kudu.Services
{
    public class DropboxHelper
    {

        public const string Dropbox = "Dropbox";
        private const string CursorKey = "dropbox_cursor";

        /// <summary>
        /// The duratio to wait between file download retries to avoid rate limiting.
        /// </summary>
        internal static TimeSpan RetryWaitToAvoidRateLimit = TimeSpan.FromSeconds(20);

        private const string AccountInfoUri = "https://api.dropbox.com/1/account/info";
        private const string DeltaApiUri = "https://api.dropbox.com/1/delta";
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

        internal IEnvironment Environment
        {
            get { return _environment; }
        }

        internal ILogger Logger
        {
            get;
            set;
        }

        public async Task UpdateDropboxDeployInfo(DropboxDeployInfo dropboxInfo)
        {
            string currentCursor = _settings.GetValue(CursorKey),
                   dropboxToken = dropboxInfo.Token;

            dropboxInfo.OldCursor = currentCursor;
            string parentPath = dropboxInfo.Path.TrimEnd('/') + '/';

            using (_tracer.Step("Updating deploy info"))
            using (var client = CreateDropboxV2HttpClient(DeltaApiUri, dropboxToken))
            {
                while (true)
                {
                    LogInfo("Fetching delta for cursor '{0}'.", currentCursor);
                    string url = String.IsNullOrEmpty(currentCursor) ? String.Empty :
                                                                      ("?cursor=" + HttpUtility.UrlPathEncode(currentCursor));
                    using (var response = await client.PostAsync(url, content: null))
                    {
                        // The Content-Type of the Json result is text/json which the JsonMediaTypeFormatter does not approve of.
                        // We'll parse it separately instead.
                        var deltaResultString = await response.EnsureSuccessStatusCode().Content.ReadAsStringAsync();
                        var deltaResult = JsonConvert.DeserializeObject<JObject>(deltaResultString);

                        var entries = deltaResult.Value<JArray>("entries");
                        if (entries != null && entries.Count > 0)
                        {
                            var filteredEntries = entries.Cast<JArray>()
                                                            .Select(DropboxEntryInfo.ParseFrom)
                                                            .Where(entry => !CanIgnoreEntry(parentPath, entry));

                            dropboxInfo.Deltas.AddRange(filteredEntries);
                        }
                        currentCursor = deltaResult.Value<string>("cursor");
                        if (!deltaResult.Value<bool>("has_more"))
                        {
                            LogInfo("Delta response exhausted. New cursor at '{0}'.", currentCursor);
                            break;
                        }
                    }
                }
            }

            dropboxInfo.NewCursor = currentCursor;
        }

        internal async Task<ChangeSet> Sync(DropboxInfo dropboxInfo, string branch, IRepository repository)
        {
            DropboxDeployInfo deployInfo = dropboxInfo.DeployInfo;

            ResetStats();

            if (_settings.GetValue(CursorKey) != deployInfo.OldCursor)
            {
                throw new InvalidOperationException(Resources.Error_MismatchDropboxCursor);
            }

            // initial sync, remove default content
            // for simplicity, we do it blindly whether or not in-place
            // given the end result is the same
            if (String.IsNullOrEmpty(deployInfo.OldCursor) && DeploymentHelper.IsDefaultWebRootContent(_environment.WebRootPath))
            {
                string hoststarthtml = Path.Combine(_environment.WebRootPath, Constants.HostingStartHtml);
                FileSystemHelpers.DeleteFileSafe(hoststarthtml);
            }

            if (!repository.IsEmpty())
            {
                // git checkout --force <branch>
                repository.Update(branch);
            }

            ChangeSet changeSet = null;
            string message = null;
            try
            {
                using (_tracer.Step("Sync with Dropbox"))
                {
                    if (dropboxInfo.OAuthVersion == 2)
                    {
                        // Fetch the deltas
                        await UpdateDropboxDeployInfo(deployInfo);
                    }

                    // Sync dropbox => repository directory
                    await ApplyChanges(dropboxInfo, useOAuth20: dropboxInfo.OAuthVersion == 2);
                }

                message = String.Format(CultureInfo.CurrentCulture,
                            Resources.Dropbox_Synchronized,
                            deployInfo.Deltas.Count);
            }
            catch (Exception)
            {
                message = String.Format(CultureInfo.CurrentCulture,
                            Resources.Dropbox_SynchronizedWithFailure,
                            _successCount,
                            deployInfo.Deltas.Count,
                            _failedCount);

                throw;
            }
            finally
            {
                Logger.Log(message);

                Logger.Log(String.Format("{0} downloaded files, {1} successful retries.", _fileCount, _retriedCount));

                IDeploymentStatusFile statusFile = _status.Open(dropboxInfo.TargetChangeset.Id);
                statusFile.UpdateMessage(message);
                statusFile.UpdateProgress(String.Format(CultureInfo.CurrentCulture, Resources.Dropbox_Committing, _successCount));

                // Commit anyway even partial change
                if (repository.Commit(message, deployInfo.UserName, deployInfo.Email ?? deployInfo.UserName))
                {
                    changeSet = repository.GetChangeSet("HEAD");
                }
            }

            // Save new dropboc cursor
            LogInfo("Update dropbox cursor");
            _settings.SetValue(CursorKey, deployInfo.NewCursor);

            return changeSet;
        }

        private async Task ApplyChanges(DropboxInfo dropboxInfo, bool useOAuth20)
        {
            DropboxDeployInfo info = dropboxInfo.DeployInfo;
            _totals = info.Deltas.Count;

            using (new Timer(UpdateStatusFile, state: dropboxInfo.TargetChangeset.Id, dueTime: TimeSpan.FromSeconds(5), period: TimeSpan.FromSeconds(5)))
            {
                await ApplyChangesCore(info, useOAuth20);
            }
        }

        internal async Task ApplyChangesCore(DropboxDeployInfo info, bool useOAuth20)
        {
            string parent = info.Path.TrimEnd('/') + '/';
            DateTime updateMessageTime = DateTime.UtcNow;
            var sem = new SemaphoreSlim(MaxConcurrentRequests, MaxConcurrentRequests);
            var rateLimiter = new RateLimiter(MaxFilesPerSecs * 10, TimeSpan.FromSeconds(10));
            var tasks = new List<Task>();

            foreach (DropboxEntryInfo entry in info.Deltas)
            {
                if (CanIgnoreEntry(parent, entry))
                {
                    continue;
                }

                var path = entry.Path;
                if (entry.IsDeleted)
                {
                    SafeDelete(parent, path);
                    Interlocked.Increment(ref _successCount);
                }
                else if (entry.IsDirectory)
                {
                    SafeCreateDir(parent, path);
                    Interlocked.Increment(ref _successCount);
                }
                else
                {
                    DateTime modified;
                    if (DateTime.TryParse(entry.Modified, out modified))
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
                        var task = Task.Run(async () =>
                        {
                            await sem.WaitAsync();
                            try
                            {
                                await rateLimiter.ThrottleAsync();
                                using (HttpClient client = useOAuth20 ? CreateDropboxV2HttpClient(DropboxApiContentUri, info.Token) :
                                                                        CreateDropboxHttpClient(info, entry))
                                {

                                    await ProcessFileAsync(client, entry.Path, parent, modified);
                                }
                            }
                            finally
                            {
                                Interlocked.Increment(ref _fileCount);
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
            await Task.WhenAll(tasks);
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
            const int DefaultBufferSize = 8192; // From http://msdn.microsoft.com/en-us/library/tyhc0kft.aspx
            var fullPath = GetRepositoryPath(parent, path);
            if (Directory.Exists(fullPath))
            {
                Directory.Delete(fullPath, recursive: true);
                LogInfo("rmdir /s " + path);
            }

            using (FileStream fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.Read, bufferSize: DefaultBufferSize, useAsync: true))
            {
                await stream.CopyToAsync(fs);
                LogInfo("write {0,6} bytes to {1}", fs.Length, path);
            }

            File.SetLastWriteTimeUtc(fullPath, lastModifiedUtc);
        }

        public virtual async Task ProcessFileAsync(HttpClient client, string path, string parent, DateTime lastModifiedUtc)
        {
            int retries = 0;
            while (retries <= MaxRetries)
            {
                try
                {
                    string requestPath = SandboxFilePath + DropboxPathEncode(path.ToLowerInvariant());
                    using (HttpResponseMessage response = await client.GetAsync(requestPath))
                    {
                        using (Stream stream = await response.EnsureSuccessStatusCode().Content.ReadAsStreamAsync())
                        {
                            await SafeWriteFile(parent, path, lastModifiedUtc, stream);
                        }
                    }
                    if (retries > 0)
                    {
                        // Increment the successful retry count
                        Interlocked.Increment(ref _retriedCount);
                    }
                    // Increment the total success counter
                    Interlocked.Increment(ref _successCount);

                    break;
                }
                catch (Exception ex)
                {
                    if (retries == MaxRetries)
                    {
                        Interlocked.Increment(ref _failedCount);
                        LogError("Get({0}) '{1}' failed with {2}", retries, SandboxFilePath + path, ex.Message);
                        throw;
                    }
                }

                // First retry is 1s, second retry is 20s assuming rate-limit
                await Task.Delay(retries == MaxRetries - 1 ? TimeSpan.FromSeconds(1) : RetryWaitToAvoidRateLimit);
                retries++;
            }
        }

        public virtual HttpClient CreateDropboxHttpClient(DropboxDeployInfo info, DropboxEntryInfo entry)
        {
            var oauthToken = GetOAuthHeader(info, entry);
            var client = new HttpClient { BaseAddress = new Uri(DropboxApiContentUri), Timeout = _timeout };
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("OAuth", oauthToken);

            return client;
        }

        public virtual HttpClient CreateDropboxV2HttpClient(string baseUrl, string oauthToken)
        {
            var client = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = _timeout };
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", oauthToken);

            return client;
        }

        private string GetRepositoryPath(string parent, string path)
        {
            string relativePath = path.Substring(parent.Length).Replace('/', '\\');
            return Path.Combine(_environment.RepositoryPath, relativePath);
        }

        private void UpdateStatusFile(object state)
        {
            string changeset = (string)state;
            IDeploymentStatusFile statusFile = _status.Open(changeset);
            statusFile.UpdateProgress(String.Format(CultureInfo.CurrentCulture,
                                        _failedCount == 0 ? Resources.Dropbox_SynchronizingProgress : Resources.Dropbox_SynchronizingProgressWithFailure,
                                        ((_successCount + _failedCount) * 100) / _totals,
                                        _totals,
                                        _failedCount));
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
                Logger.Log(message, LogEntryType.Error);
                _tracer.TraceError(message);
            }
        }

        private void ResetStats()
        {
            _totals = 0;
            _successCount = 0;
            _fileCount = 0;
            _failedCount = 0;
            _retriedCount = 0;
        }

        private static string GetOAuthHeader(DropboxDeployInfo info, DropboxEntryInfo delta)
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
            foreach (var item in parameters)
            {
                if (sb.Length != 0)
                {
                    sb.Append(',');
                }
                sb.AppendFormat("{0}=\"{1}\"", item.Key, item.Value);
            }
            return sb.ToString();
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

        /// <summary>
        /// An entry can be ignored if it doesn't belong to the path of the synced app or 
        /// if it looks like a tool generated folder (e.g. .hg or .git)
        /// </summary>
        private static bool CanIgnoreEntry(string parent, DropboxEntryInfo entry)
        {
            if (!entry.Path.StartsWith(parent, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Ignore .git and .hg files and folders
            string pathWithSlash = entry.Path + "/";
            if (pathWithSlash.IndexOf("/.git/", StringComparison.OrdinalIgnoreCase) != -1 ||
                pathWithSlash.IndexOf("/.hg/", StringComparison.OrdinalIgnoreCase) != -1)
            {
                return true;
            }

            return false;
        }
    }
}
