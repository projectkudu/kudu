using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
using Kudu.Core.Tracing;

namespace Kudu.Services.FetchHelpers
{
    internal class OneDriveHelper
    {
        private const string JsonMediaType = "application/json";
        private const string FormUrlEncodedMediaType = "application/x-www-form-urlencoded";
        private const string UserAgentHeader = "User-Agent";
        private const string MicrosoftAzure = "Microsoft Azure";
        private const string CursorKey = "onedrive_cursor";
        private const int DefaultBufferSize = 8192; // From http://msdn.microsoft.com/en-us/library/tyhc0kft.aspx
        private const int MaxRetryCount = 3;
        private const int MaxFilesPerSeconds = 5;

        private readonly ITracer _tracer;
        private readonly IDeploymentSettingsManager _settings;
        private readonly IEnvironment _environment;

        internal ILogger Logger
        {
            get;
            set;
        }

        public OneDriveHelper(ITracer tracer,
            IDeploymentSettingsManager settings,
            IEnvironment environment)
        {
            _tracer = tracer;
            _settings = settings;
            _environment = environment;
        }

        internal async Task Sync(OneDriveInfo info)
        {
            string cursor = _settings.GetValue(CursorKey);
            ChangesResult changes = null;
            using (_tracer.Step("Getting delta changes with cursor: {0} ...", cursor))
            {
                changes = await GetChanges(info.AccessToken, info.RepositoryUrl, cursor);
            }

            if (changes == null || changes.Count == 0)
            {
                _tracer.Trace("No changes need to be applied.");
                LogMessage(Resources.OneDriveNoChangesFound);
                return;
            }

            using (_tracer.Step("Applying {0} changes ...", changes.Count))
            {
                LogMessage(Resources.OneDriveApplyingChanges, changes.Count);

                // perform action seperately, so that can ensure timestamp on directory
                // e.g two changes:
                //  (new) file /a/b/c.txt
                //  (new) dir  /a/b
                //  if created dir first then create file. file creation will trigger folder timestamp change.
                //  which will result in "/a/b" has timestamp from the monent of file creation instead of the timestamp value from server, where value supposed to be set by code specifically.
                ApplyResult deletionResult = await ApplyChangesParallel(changes.DeletionChanges, info.AccessToken, maxParallelCount: 1);
                ApplyResult fileChangeResult = await ApplyChangesParallel(changes.FileChanges, info.AccessToken, maxParallelCount: System.Environment.ProcessorCount);
                // apply folder changes at last to maintain same timestamp as in OneDrive
                ApplyResult directoryChangeResult = await ApplyChangesParallel(changes.DirectoryChanges, info.AccessToken, maxParallelCount: 1);

                int totalSuccessCount = deletionResult.SuccessCount + fileChangeResult.SuccessCount + directoryChangeResult.SuccessCount;
                int totalFailCount = deletionResult.FailureCount + fileChangeResult.FailureCount + directoryChangeResult.FailureCount;
                _tracer.Trace("{0} successed, {1} failed", totalSuccessCount, totalFailCount);
                LogMessage(Resources.OneDriveApplyResult, totalSuccessCount, totalFailCount);

                if (totalFailCount > 0)
                {
                    // signal deployment failied
                    throw new Exception(string.Format(CultureInfo.CurrentUICulture, Resources.OneDriveApplyResult, totalSuccessCount, totalFailCount));
                }
            }

            // finally keep a copy of the new cursor
            _settings.SetValue(CursorKey, changes.Cursor);
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

        private static HttpClient CreateHttpClient(string accessToken)
        {
            HttpClient client = new HttpClient();
            client.MaxResponseContentBufferSize = 1024 * 1024 * 10;
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(JsonMediaType));
            if (!client.DefaultRequestHeaders.Contains(UserAgentHeader))
            {
                client.DefaultRequestHeaders.Add(UserAgentHeader, MicrosoftAzure);
            }

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", accessToken);

            return client;
        }

        private async Task<T> ProcessResponse<T>(string operation, HttpResponseMessage response)
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
                throw new ArgumentException(string.Format(CultureInfo.CurrentUICulture, Resources.OneDriveInvalidRequestUri, requestUri));
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

        private async Task<ChangesResult> GetChanges(string accessToken, string requestUri, string cursor)
        {
            string rootUri = GetOneDriveRootUri(requestUri);
            requestUri = string.Format(CultureInfo.InvariantCulture, "{0}/{1}", await GetItemUri(accessToken, requestUri, rootUri), "view.changes");

            ChangesResult result = new ChangesResult();
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

                    using (var response = await client.GetAsync(uri))
                    {
                        changes = await ProcessResponse<Dictionary<string, object>>("GetChanges", response);
                    }

                    if (changes.ContainsKey("@changes.resync"))
                    {
                        if (string.IsNullOrEmpty(next))
                        {
                            throw new InvalidOperationException(string.Format(CultureInfo.CurrentUICulture, Resources.OneDriveUnableToSync, changes["@changes.resync"]));
                        }

                        // resync
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

        private async Task<ApplyResult> ApplyChangesParallel(OneDriveModel.OneDriveChangeCollection changes, string accessToken, int maxParallelCount)
        {
            List<Task> tasks = new List<Task>();
            string wwwroot = _environment.WebRootPath;
            int failureCount = 0;
            int successCount = 0;

            using (var sem = new SemaphoreSlim(initialCount: maxParallelCount, maxCount: maxParallelCount))
            using (var filesPerSecLimiter = new RateLimiter(MaxFilesPerSeconds, TimeSpan.FromSeconds(1)))
            {
                foreach (OneDriveModel.OneDriveChange change in changes)
                {
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            await sem.WaitAsync();
                            await filesPerSecLimiter.ThrottleAsync();
                            bool applied = await ProcessChangeWithRetry(change, accessToken, wwwroot);
                            if (applied)
                            {
                                Interlocked.Increment(ref successCount);
                            }
                            else
                            {
                                Interlocked.Increment(ref failureCount);
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

            return new ApplyResult()
            {
                FailureCount = failureCount,
                SuccessCount = successCount
            };
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

            // TODO: hande rename and move

            return updated;
        }

        /// <summary>
        /// Handle both file and folder deletion, and ignore not found exception
        /// </summary>
        private void HandlingDeletion(OneDriveModel.OneDriveChange change, string wwwroot)
        {
            if (!change.IsDeleted)
                return;

            string fullPath = Path.Combine(wwwroot, change.Path);

            try
            {
                if (FileSystemHelpers.FileExists(fullPath))
                {
                    FileSystemHelpers.DeleteFile(fullPath);
                    TraceMessage("Deleted file {0}", change.Path);
                    LogMessage(Resources.OneDriveDeletedFile, fullPath);
                }
                else if (FileSystemHelpers.DirectoryExists(fullPath))
                {
                    FileSystemHelpers.DeleteDirectorySafe(fullPath, ignoreErrors: false);
                    TraceMessage("Deleted directory {0}", change.Path);
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

            string fullPath = Path.Combine(wwwroot, change.Path);
            if (change.IsFile)
            {
                // return "1/1/1601 12:00:00 AM" when file not existed
                DateTime lastWriteTime = FileSystemHelpers.GetLastWriteTimeUtc(fullPath);
                if (lastWriteTime != change.LastModifiedUtc)
                {
                    string parentDir = Path.GetDirectoryName(fullPath);
                    FileSystemHelpers.EnsureDirectory(parentDir);

                    if (FileSystemHelpers.FileExists(fullPath))
                    {
                        TraceMessage("Updating file {0} ...", fullPath);
                    }
                    else
                    {
                        TraceMessage("Creating file {0} ...", fullPath);
                    }

                    using (var client = CreateHttpClient(accessToken))
                    using (HttpResponseMessage response = await client.GetAsync(change.ContentUri))
                    {
                        var fileResponse = await ProcessResponse<Dictionary<string, object>>("HandlingUpdateOrCreate", response);
                        string downloadUrl = (string)fileResponse["@content.downloadUrl"];

                        using (HttpResponseMessage downloadResponse = await client.GetAsync(downloadUrl))
                        using (FileStream fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.Read, bufferSize: DefaultBufferSize, useAsync: true))
                        using (Stream stream = await downloadResponse.EnsureSuccessStatusCode().Content.ReadAsStreamAsync())
                        {
                            await stream.CopyToAsync(fs);
                        }
                    }

                    FileSystemHelpers.SetLastWriteTimeUtc(fullPath, change.LastModifiedUtc);
                }
                else
                {
                    _tracer.Trace("Timestamp not changed. Skip updating file {0}", fullPath);
                }
            }
            else
            {
                if (!FileSystemHelpers.DirectoryExists(fullPath))
                {
                    TraceMessage("Creating directory {0} ...", fullPath);
                    FileSystemHelpers.CreateDirectory(fullPath);
                }
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

        internal class ApplyResult
        {
            internal int FailureCount { get; set; }
            internal int SuccessCount { get; set; }
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
