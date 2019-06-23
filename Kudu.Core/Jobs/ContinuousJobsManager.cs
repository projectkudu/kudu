using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using Kudu.Contracts.Jobs;
using Kudu.Contracts.Settings;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;
using System.Net;
using System.Threading.Tasks;
using Kudu.Contracts.Tracing;

namespace Kudu.Core.Jobs
{
    public class ContinuousJobsManager : JobsManagerBase<ContinuousJob>, IContinuousJobsManager
    {
        private const string StatusFilesSearchPattern = ContinuousJobStatus.FileNamePrefix + "*";
        private const string Localhost = "127.0.0.1";
        private const string HttpScheme = "http";

        private readonly Dictionary<string, ContinuousJobRunner> _continuousJobRunners = new Dictionary<string, ContinuousJobRunner>(StringComparer.OrdinalIgnoreCase);

        public ContinuousJobsManager(string basePath, ITraceFactory traceFactory, IEnvironment environment, IDeploymentSettingsManager settings, IAnalytics analytics, IEnumerable<string> excludedJobsNames = null)
            : base(traceFactory, environment, settings, analytics, Constants.ContinuousPath, basePath, excludedJobsNames)
        {
            RegisterExtraEventHandlerForFileChange(OnJobChanged);
        }

        public override ContinuousJob GetJob(string jobName)
        {
            return GetJobInternal(jobName);
        }

        public void DisableJob(string jobName)
        {
            var continuousJobRunner = GetJobRunner(jobName);
            continuousJobRunner.DisableJob();
        }

        public void EnableJob(string jobName)
        {
            ContinuousJob continuousJob = GetJob(jobName);
            if (continuousJob == null)
            {
                throw new JobNotFoundException($"Cannot find '{jobName}' continuous job");
            }

            ContinuousJobRunner continuousJobRunner;
            if (!_continuousJobRunners.TryGetValue(continuousJob.Name, out continuousJobRunner))
            {
                throw new JobNotFoundException($"Missing job runner for '{jobName}' continuous job");
            }

            continuousJobRunner.EnableJob();
        }

        public async Task<HttpResponseMessage> HandleRequest(string jobName, string path, HttpRequestMessage request)
        {
            using (var client = new HttpClient())
            {
                var jobRunner = GetJobRunner(jobName);
                var forwardRequest = GetForwardRequest(jobRunner.WebJobPort, path, request);
                return await client.SendAsync(forwardRequest);
            }
        }

        public static HttpRequestMessage GetForwardRequest(int port, string path, HttpRequestMessage original)
        {
            var cloneUriBuilder = new UriBuilder(HttpScheme, Localhost, port)
            {
                Path = path,
                // Uri has a ? leading the query string, while UriBuilder doesn't expect a leading ?
                // If we don't trim it, we'll endup with ?? in the URL
                Query = original.RequestUri.Query.Trim('?')
            };

            var clone = new HttpRequestMessage(original.Method, cloneUriBuilder.Uri);

            if (original.Method != HttpMethod.Get)
            {
                clone.Content = original.Content;
            }

            original.Headers.Aggregate(clone.Headers, (a, b) =>
            {
                if (!b.Key.Equals("HOST", StringComparison.OrdinalIgnoreCase))
                {
                    a.Add(b.Key, b.Value);
                }
                return a;
            });

            return clone;
        }

        private ContinuousJobRunner GetJobRunner(string jobName)
        {
            ContinuousJobRunner continuousJobRunner;
            if (!_continuousJobRunners.TryGetValue(jobName, out continuousJobRunner))
            {
                throw new JobNotFoundException($"Missing job runner for '{jobName}' continuous job");
            }
            return continuousJobRunner;
        }

        protected override void OnShutdown()
        {
            JobsWatcher.Stop();

            foreach (ContinuousJobRunner continuousJobRunner in _continuousJobRunners.Values)
            {
                continuousJobRunner.StopJob(isShutdown: true);
            }
        }

        protected override void UpdateJob(ContinuousJob job)
        {
            string jobsSpecificDataPath = Path.Combine(JobsDataPath, job.Name);
            FileSystemHelpers.EnsureDirectory(jobsSpecificDataPath);

            job.LogUrl = BuildLogUrl(job.Name);
            UpdateDetailedStatus(job, jobsSpecificDataPath);
        }

        /// <summary>
        /// Update the status and the detailed status of the job.
        /// The status is the status of one of the instances,
        /// The detailed status contains status for all instances and looks like:
        ///
        /// aabbcc - Running
        /// 112233 - PendingRestart
        ///
        /// </summary>
        private void UpdateDetailedStatus(ContinuousJob job, string jobsSpecificDataPath)
        {
            string instanceId = InstanceIdUtility.GetShortInstanceId();

            string[] statusFiles = FileSystemHelpers.GetFiles(jobsSpecificDataPath, StatusFilesSearchPattern);
            if (statusFiles.Length <= 0)
            {
                // If no status files exist update to default values
                string status = ContinuousJobStatus.Initializing.Status;

                job.DetailedStatus = instanceId + " - " + status;
                job.Status = status;

                return;
            }

            string lastStatus = null;
            var stringBuilder = new StringBuilder();

            foreach (string statusFile in statusFiles)
            {
                // Extract instance id from the file name
                int statusFileNameIndex = statusFile.IndexOf(ContinuousJobStatus.FileNamePrefix, StringComparison.OrdinalIgnoreCase);
                string statusFileInstanceId = statusFile.Substring(statusFileNameIndex + ContinuousJobStatus.FileNamePrefix.Length);

                // Try to delete file, it'll be deleted if no one holds a lock on it
                // That way we know the status file is obsolete
                if (String.Equals(statusFileInstanceId, instanceId, StringComparison.OrdinalIgnoreCase)
                    || !TryDelete(job, statusFile))
                {
                    // If we couldn't delete the file, we know it holds the status of an actual instance holding it
                    var continuousJobStatus = GetStatus<ContinuousJobStatus>(statusFile) ?? ContinuousJobStatus.Initializing;

                    stringBuilder.AppendLine(statusFileInstanceId + " - " + continuousJobStatus.Status);
                    if (lastStatus == null ||
                        !ContinuousJobStatus.InactiveInstance.Equals(continuousJobStatus))
                    {
                        lastStatus = continuousJobStatus.Status;
                    }
                }
            }

            // Job status will only show one of the statuses
            job.Status = lastStatus;
            job.DetailedStatus = stringBuilder.ToString();
        }

        private bool TryDelete(ContinuousJob job, string statusFile)
        {
            try
            {
                FileSystemHelpers.DeleteFile(statusFile);
                Analytics.JobEvent(job.Name, string.Format("Delete stale status file {0}", statusFile), job.JobType, string.Empty);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private Uri BuildLogUrl(string jobName)
        {
            return BuildVfsUrl("{0}/{1}".FormatInvariant(jobName, ContinuousJobLogger.JobLogFileName));
        }

        private void OnJobChanged(string jobName)
        {
            ContinuousJob continuousJob = GetJob(jobName);
            if (continuousJob == null || !String.IsNullOrEmpty(continuousJob.Error))
            {
                RemoveJob(jobName);
            }
            else
            {
                RefreshJob(continuousJob, logRefresh: !JobsWatcher.FirstTimeMakingChanges);
            }
        }

        private void RefreshJob(ContinuousJob continuousJob, bool logRefresh)
        {
            ContinuousJobRunner continuousJobRunner;
            if (!_continuousJobRunners.TryGetValue(continuousJob.Name, out continuousJobRunner))
            {
                continuousJobRunner = new ContinuousJobRunner(continuousJob, JobsBinariesPath, Environment, Settings, TraceFactory, Analytics);
                _continuousJobRunners.Add(continuousJob.Name, continuousJobRunner);
            }
            else
            {
                // job may change due to network transient.  In such case,
                // we may lose and need to re-acquire the instance status file lock.
                continuousJobRunner.ResetLockedStatusFile();
            }

            JobSettings jobSettings = continuousJob.Settings;
            continuousJobRunner.RefreshJob(continuousJob, jobSettings, logRefresh);
        }

        private void RemoveJob(string updatedJobName)
        {
            ContinuousJobRunner continuousJobRunner;
            if (!_continuousJobRunners.TryGetValue(updatedJobName, out continuousJobRunner))
            {
                return;
            }

            continuousJobRunner.StopJob();
            continuousJobRunner.Dispose();

            _continuousJobRunners.Remove(updatedJobName);
        }

        protected override IEnumerable<string> ListJobNames(bool forceRefreshCache)
        {
            IEnumerable<ContinuousJob> continuousJobs = ListJobs(forceRefreshCache);
            return _continuousJobRunners.Keys.Union(continuousJobs.Select(j => j.Name));
        }
    }
}