using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Core.Deployment;
using Kudu.Core.Infrastructure;
using Kudu.Core.SourceControl;
using Kudu.Core.Tracing;

namespace Kudu.Services.FetchHelpers
{
    public class OneDriveHelper
    {
        private const string JsonMediaType = "application/json";
        private const string FormUrlEncodedMediaType = "application/x-www-form-urlencoded";
        private const string UserAgentHeader = "User-Agent";
        private const string MicrosoftAzure = "Microsoft Azure";
        private const string CursorKey = "onedrive_cursor";
        private const int DefaultBufferSize = 8192; // From http://msdn.microsoft.com/en-us/library/tyhc0kft.aspx
        // From experiment, this concurrent seems to be best optimal in success rate and speed.
        private const int MaxConcurrentRequests = 6;
        private const int MaxRetryCount = 3;
        // if we exceed this failure (considered unrecoverable - token expired), we will bail out.
        private const int MaxFailures = 10;

        private ITracer _tracer;
        private readonly IDeploymentStatusManager _status;
        private readonly IDeploymentSettingsManager _settings;
        private readonly IEnvironment _environment;
        private int _failedCount = 0;
        private int _successCount = 0;
        private int _totals = 0;

        internal ILogger Logger
        {
            get;
            set;
        }

        public OneDriveHelper(ITracer tracer,
                              IDeploymentStatusManager status,
                              IDeploymentSettingsManager settings,
                              IEnvironment environment)
        {
            _tracer = tracer;
            _status = status;
            _settings = settings;
            _environment = environment;
        }

        internal async Task<ChangeSet> Sync(OneDriveInfo info, IRepository repository, ITracer tracer)
        {
            ChangeSet changeSet = null;
            string cursor = _settings.GetValue(CursorKey);
            ChangesResult changes = null;

            // use incoming tracer since it is background work
            _tracer = tracer;

            // We truncate cursor value in filename but keep it unharmed in the trace content
            using (_tracer.Step("Getting delta changes with cursor: {0}...", cursor.Truncate(5)))
            using (_tracer.Step("cursor: {0}", cursor))
            {
                changes = await GetChanges(info.TargetChangeset.Id, info.AccessToken, info.RepositoryUrl, cursor);
            }

            if (changes == null || changes.Count == 0)
            {
                _tracer.Trace("No changes need to be applied.");
                LogMessage(Resources.OneDriveNoChangesFound);
                return changeSet;
            }

            // for simplicity, use file changes as effective total
            _totals = changes.FileChanges.Count > 0 ? changes.FileChanges.Count : changes.Count;

            string hoststarthtml = Path.Combine(_environment.WebRootPath, Constants.HostingStartHtml);
            FileSystemHelpers.DeleteFileSafe(hoststarthtml);

            using (new Timer(UpdateStatusFile, state: info.TargetChangeset.Id, dueTime: TimeSpan.FromSeconds(5), period: TimeSpan.FromSeconds(5)))
            using (_tracer.Step("Applying {0} changes ...", _totals))
            {
                LogMessage(Resources.OneDriveApplyingChanges, _totals);

                // perform action separately, so that can ensure timestamp on directory
                // e.g two changes:
                //  (new) file /a/b/c.txt
                //  (new) dir  /a/b
                //  if created dir first then create file. file creation will trigger folder timestamp change.
                //  which will result in "/a/b" has timestamp from the monent of file creation instead of the timestamp value from server, where value supposed to be set by code specifically.
                await ApplyChangesParallel(changes.DeletionChanges, info.AccessToken, maxParallelCount: 1, countSuccess: changes.FileChanges.Count == 0);
                await ApplyChangesParallel(changes.FileChanges, info.AccessToken, maxParallelCount: MaxConcurrentRequests, countSuccess: true);
                // apply folder changes at last to maintain same timestamp as in OneDrive
                await ApplyChangesParallel(changes.DirectoryChanges, info.AccessToken, maxParallelCount: 1, countSuccess: changes.FileChanges.Count == 0);

                _tracer.Trace("{0} succeeded, {1} failed", _successCount, _failedCount);
                LogMessage(Resources.OneDriveApplyResult, _successCount, _failedCount);

                string message = _failedCount > 0 ?
                    string.Format(CultureInfo.CurrentCulture, Resources.OneDrive_SynchronizedWithFailure, _successCount, _totals, _failedCount) :
                    string.Format(CultureInfo.CurrentCulture, Resources.OneDrive_Synchronized, _totals);

                // Commit anyway even partial change
                if (repository.Commit(message, info.AuthorName, info.AuthorEmail))
                {
                    changeSet = repository.GetChangeSet("HEAD");
                }

                if (_failedCount > 0)
                {
                    // signal deployment failied
                    throw new Exception(string.Format(CultureInfo.CurrentCulture, Resources.OneDriveApplyResult, _successCount, _failedCount));
                }
            }

            // finally keep a copy of the new cursor
            _settings.SetValue(CursorKey, changes.Cursor);

            return changeSet;
        }

        private void UpdateStatusFile(object state)
        {
            string changeset = (string)state;
            IDeploymentStatusFile statusFile = _status.Open(changeset);
            statusFile.UpdateProgress(string.Format(CultureInfo.CurrentCulture,
                                        _failedCount == 0 ? Resources.OneDrive_SynchronizingProgress : Resources.OneDrive_SynchronizingProgressWithFailure,
                                        ((_successCount + _failedCount) * 100) / _totals,
                                        _totals,
                                        _failedCount));
        }

        private void LogMessage(string format, params object[] args)
        {
            if (Logger != null)
            {
                Logger.Log(format, args);
            }
        }

        private void TraceMessage(string format, params object[] args)
        {
            lock (_tracer)
            {
                _tracer.Trace(format, args);
            }
        }

        public virtual HttpClient CreateHttpClient(string accessToken)
        {
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(JsonMediaType));
            if (!client.DefaultRequestHeaders.Contains(UserAgentHeader))
            {
                client.DefaultRequestHeaders.Add(UserAgentHeader, MicrosoftAzure);
            }

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", accessToken);

            return client;
        }

        private static async Task<T> ProcessResponse<T>(string operation, HttpResponseMessage response)
        {
            string content = response.Content != null ? await response.Content.ReadAsStringAsync() : null;
            if (response.IsSuccessStatusCode)
            {
                var serializer = new JavaScriptSerializer();
                return serializer.Deserialize<T>(content);
            }

            throw new Exception(string.Format(CultureInfo.InvariantCulture, "'{0}' : '{1}', {2}", response.StatusCode, operation, content));
        }

        private static string GetOneDriveRootUri(string requestUri)
        {
            // should come in as https://api.onedrive.com/v1.0/drive/special/approot:/{folder name}
            var uriTokens = requestUri.Split(new string[] { "/special/" }, StringSplitOptions.None);
            if (uriTokens.Length != 2)
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Resources.OneDriveInvalidRequestUri, requestUri));
            }

            return uriTokens[0];
        }

        private async Task<string> GetItemUri(string accessToken, string requestUri, string rootUri)
        {
            using (_tracer.Step("Converting {0} to identify by id", requestUri))
            {
                using (var client = CreateHttpClient(accessToken))
                using (var response = await client.GetAsync(requestUri))
                {
                    var item = await ProcessResponse<OneDriveModel.OneDriveItem>("GetItemUri", response);

                    // should return as https://api.onedrive.com/v1.0/drive/items/{folder id}
                    return string.Format(CultureInfo.InvariantCulture, "{0}/items/{1}", rootUri, item.id);
                }
            }
        }

        private async Task<ChangesResult> GetChanges(string changeset, string accessToken, string requestUri, string cursor)
        {
            string rootUri = GetOneDriveRootUri(requestUri);
            requestUri = string.Format(CultureInfo.InvariantCulture, "{0}/{1}", await GetItemUri(accessToken, requestUri, rootUri), "view.changes");

            ChangesResult result = new ChangesResult();
            IDeploymentStatusFile statusFile = _status.Open(changeset);
            var loop = 0;
            using (var client = CreateHttpClient(accessToken))
            {
                var next = cursor;
                var ids = new Dictionary<string, OneDriveModel.ItemInfo>();
                Dictionary<string, object> changes = null;
                var serializer = new JavaScriptSerializer();
                do
                {
                    var uri = requestUri;
                    if (!string.IsNullOrWhiteSpace(next))
                    {
                        uri = string.Format(CultureInfo.InvariantCulture, "{0}?token={1}", requestUri, next);
                    }

                    ++loop;
                    statusFile.UpdateProgress(string.Format(CultureInfo.CurrentCulture, "Getting delta changes batch #{0}", loop));
                    using (_tracer.Step("Getting delta changes batch #{0}", loop))
                    {
                        using (var response = await client.GetAsync(uri))
                        {
                            changes = await ProcessResponse<Dictionary<string, object>>("GetChanges", response);
                        }
                    }

                    if (changes.ContainsKey("@changes.resync"))
                    {
                        if (string.IsNullOrEmpty(next))
                        {
                            throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Resources.OneDriveUnableToSync, changes["@changes.resync"]));
                        }

                        // resync
                        loop = 0;
                        next = null;
                        changes["@changes.hasMoreChanges"] = true;
                        result = new ChangesResult();
                        continue;
                    }

                    var items = serializer.ConvertToType<OneDriveModel.OneDriveItemCollection>(changes);

                    // changes
                    if (items != null && items.value != null && items.value.Count > 0)
                    {
                        var subResults = GetChanges(items, ids, rootUri);
                        result.DeletionChanges.AddRange(subResults.DeletionChanges);
                        result.DirectoryChanges.AddRange(subResults.DirectoryChanges);
                        result.FileChanges.AddRange(subResults.FileChanges);
                    }

                    // set next token
                    next = (string)changes["@changes.token"];

                } while ((bool)changes["@changes.hasMoreChanges"]);

                result.Cursor = next;
                return result;
            }
        }

        private static ChangesResult GetChanges(OneDriveModel.OneDriveItemCollection items, Dictionary<string, OneDriveModel.ItemInfo> ids, string rootUri)
        {
            ChangesResult result = new ChangesResult();

            foreach (var item in items.value)
            {
                ids[item.id] = new OneDriveModel.ItemInfo
                {
                    name = item.name,
                    parentId = item.parentReference.id
                };

                var path = GetPath(ids, item);
                if (item.deleted != null)
                {
                    result.DeletionChanges.Add(new OneDriveModel.OneDriveChange { Path = path, IsDeleted = true });
                }
                else
                {
                    var change = new OneDriveModel.OneDriveChange
                    {
                        ContentUri = string.Format(CultureInfo.InvariantCulture, "{0}/items/{1}", rootUri, item.id),
                        Path = path,
                        IsFile = item.file != null,
                        LastModifiedUtc = item.lastModifiedDateTime.ToUniversalTime()
                    };

                    if (change.IsFile)
                    {
                        result.FileChanges.Add(change);
                    }
                    else
                    {
                        result.DirectoryChanges.Add(change);
                    }
                }
            }

            return result;
        }

        private static string GetPath(Dictionary<string, OneDriveModel.ItemInfo> ids, OneDriveModel.OneDriveItem item)
        {
            var path = item.name;
            var parentId = item.parentReference.id;
            while (true)
            {
                OneDriveModel.ItemInfo info;
                if (!ids.TryGetValue(parentId, out info))
                {
                    return path;
                }

                path = string.Format(CultureInfo.InvariantCulture, @"{0}\{1}", info.name, path);
                parentId = info.parentId;
            }
        }

        private async Task ApplyChangesParallel(OneDriveModel.OneDriveChangeCollection changes, string accessToken, int maxParallelCount, bool countSuccess)
        {
            List<Task> tasks = new List<Task>();
            string wwwroot = _environment.WebRootPath;
            bool failureExceed = false;

            using (var sem = new SemaphoreSlim(initialCount: maxParallelCount, maxCount: maxParallelCount))
            {
                foreach (OneDriveModel.OneDriveChange change in changes)
                {
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            await sem.WaitAsync();
                            if (failureExceed)
                            {
                                return;
                            }

                            bool applied = await ProcessChangeWithRetry(change, accessToken, wwwroot);
                            if (applied)
                            {
                                if (countSuccess)
                                {
                                    Interlocked.Increment(ref _successCount);
                                }
                            }
                            else
                            {
                                if (Interlocked.Increment(ref _failedCount) > MaxFailures)
                                {
                                    failureExceed = true;
                                }
                            }
                        }
                        finally
                        {
                            sem.Release();
                        }
                    }));
                }

                await Task.WhenAll(tasks);
            }
        }

        private async Task<bool> ProcessChangeWithRetry(OneDriveModel.OneDriveChange change, string accessToken, string wwwroot)
        {
            try
            {
                HandlingDeletion(change, wwwroot);
            }
            catch (Exception ex)
            {
                _tracer.TraceError(ex, "HandlingDeletion");
                return false;
            }

            bool updated = await RetryHandler(() =>
            {
                return HandlingUpdateOrCreate(change, wwwroot, accessToken);
            });

            // TODO: handle rename and move

            return updated;
        }

        /// <summary>
        /// Handle both file and folder deletion, and ignore not found exception
        /// </summary>
        private void HandlingDeletion(OneDriveModel.OneDriveChange change, string wwwroot)
        {
            if (!change.IsDeleted)
                return;

            string fullPath = GetDestinationPath(wwwroot, change.Path);
            if (fullPath == null)
            {
                TraceMessage("Ignore folder {0}", change.Path);
                return;
            }

            try
            {
                if (FileSystemHelpers.FileExists(fullPath))
                {
                    FileSystemHelpers.DeleteFile(fullPath);
                    TraceMessage("Deleted file {0}", fullPath);
                    LogMessage(Resources.OneDriveDeletedFile, fullPath);
                }
                else if (FileSystemHelpers.DirectoryExists(fullPath))
                {
                    FileSystemHelpers.DeleteDirectorySafe(fullPath, ignoreErrors: false);
                    TraceMessage("Deleted directory {0}", fullPath);
                    LogMessage(Resources.OneDriveDeletedDirectory, fullPath);
                }
                else
                {
                    TraceMessage("Not found: {0}. Unknown item type, skip deletion!", fullPath);
                }
            }
            catch (DirectoryNotFoundException)
            {
                TraceMessage("Directory Not found: {0}. Skip deletion!", fullPath);
            }
            catch (FileNotFoundException)
            {
                TraceMessage("File Not found: {0}. Skip deletion!", fullPath);
            }
        }

        /// <summary>
        /// <para>Handle both file and folder create/update</para>
        /// <para>If local file timestamp the same as the file in server, assume file/folder unchanged, will skip action.</para>
        /// </summary>
        private async Task HandlingUpdateOrCreate(OneDriveModel.OneDriveChange change, string wwwroot, string accessToken)
        {
            if (change.IsDeleted)
                return;

            string fullPath = GetDestinationPath(wwwroot, change.Path);
            if (fullPath == null)
            {
                TraceMessage("Ignore folder {0}", change.Path);
                return;
            }

            if (change.IsFile)
            {
                // return "1/1/1601 12:00:00 AM" when file not existed
                DateTime lastWriteTime = FileSystemHelpers.GetLastWriteTimeUtc(fullPath);
                if (lastWriteTime != change.LastModifiedUtc)
                {
                    string parentDir = Path.GetDirectoryName(fullPath);
                    FileSystemHelpers.EnsureDirectory(parentDir);

                    var now = DateTime.UtcNow;
                    var success = false;
                    try
                    {
                        using (var client = CreateHttpClient(accessToken))
                        using (HttpResponseMessage response = await client.GetAsync(change.ContentUri))
                        {
                            var fileResponse = await ProcessResponse<Dictionary<string, object>>("HandlingUpdateOrCreate", response);
                            string downloadUrl = (string)fileResponse["@content.downloadUrl"];

                            using (HttpResponseMessage downloadResponse = await client.GetAsync(downloadUrl))
                            using (Stream stream = await downloadResponse.EnsureSuccessStatusCode().Content.ReadAsStreamAsync())
                            {
                                await WriteToFile(stream, fullPath);
                            }
                        }

                        FileSystemHelpers.SetLastWriteTimeUtc(fullPath, change.LastModifiedUtc);

                        success = true;
                    }
                    finally
                    {
                        var elapse = (DateTime.UtcNow - now).TotalMilliseconds;
                        var result = success ? "successful" : "failed";
                        if (FileSystemHelpers.FileExists(fullPath))
                        {
                            TraceMessage("Update file {0} {1} ({2:0}ms)", fullPath, result, elapse);
                        }
                        else
                        {
                            TraceMessage("Create file {0} {1} ({2:0}ms)", fullPath, result, elapse);
                        }
                    }
                }
                else
                {
                    _tracer.Trace("Timestamp not changed. Skip updating file {0}", fullPath);
                }
            }
            else
            {
                if (FileSystemHelpers.DirectoryExists(fullPath))
                {
                    TraceMessage("Directory {0} exists, no action performed.", fullPath);
                }
                else
                {
                    TraceMessage("Creating directory {0} ...", fullPath);
                    FileSystemHelpers.CreateDirectory(fullPath);
                }
            }
        }

        /// <summary>
        /// Make this method "public virtual" to workaround the fact that we cannot mock FileStream
        /// </summary>
        public virtual async Task WriteToFile(Stream srcStream, string targetFilePath)
        {
            using (FileStream fs = new FileStream(targetFilePath, FileMode.Create, FileAccess.Write, FileShare.Read, bufferSize: DefaultBufferSize, useAsync: true))
            {
                await srcStream.CopyToAsync(fs);
            }
        }

        private async Task<bool> RetryHandler(Func<Task> action)
        {
            int retryCount = 0;
            while (retryCount < MaxRetryCount)
            {
                try
                {
                    await action();
                    return true;
                }
                catch (Exception ex)
                {
                    _tracer.TraceError(ex, "RetryError");
                    retryCount++;
                }

                await Task.Delay(TimeSpan.FromSeconds(retryCount));
            }

            return false;
        }

        private static string GetDestinationPath(string wwwroot, string path)
        {
            // <para>We sync content under "https://api.onedrive.com/v1.0/drive/special/approot:/folder" to wwwroot</para>
            // <para>File path in OneDrive start with root folder, e.g folder\index.html</para>
            // <para>When downloaded to wwwroot, we should see wwwroot\index.html</para>
            int slashIndex = path.IndexOf('\\');
            if (slashIndex >= 0)
            {
                string changePathWithoutRoot = path.Substring(slashIndex + 1);
                return Path.Combine(wwwroot, changePathWithoutRoot);
            }

            return null;
        }

        internal class ChangesResult
        {
            internal string Cursor { get; set; }
            internal OneDriveModel.OneDriveChangeCollection FileChanges { get; set; }
            internal OneDriveModel.OneDriveChangeCollection DirectoryChanges { get; set; }
            internal OneDriveModel.OneDriveChangeCollection DeletionChanges { get; set; }

            internal int Count
            {
                get
                {
                    return FileChanges.Count + DirectoryChanges.Count + DeletionChanges.Count;
                }
            }

            internal ChangesResult()
            {
                FileChanges = new OneDriveModel.OneDriveChangeCollection();
                DirectoryChanges = new OneDriveModel.OneDriveChangeCollection();
                DeletionChanges = new OneDriveModel.OneDriveChangeCollection();
            }
        }
    }
}
