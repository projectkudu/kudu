using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Kudu.Contracts.Jobs;
using Kudu.Contracts.Settings;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;

namespace Kudu.Core.Jobs
{
    public class ContinuousJobsManager : JobsManagerBase<ContinuousJob>, IContinuousJobsManager, IDisposable
    {
        private const string StatusFilesSearchPattern = ContinuousJobStatus.FileNamePrefix + "*";

        private readonly Dictionary<string, ContinuousJobRunner> _continuousJobRunners = new Dictionary<string, ContinuousJobRunner>(StringComparer.OrdinalIgnoreCase);

        private JobsFileWatcher _jobsFileWatcher;

        public ContinuousJobsManager(ITraceFactory traceFactory, IEnvironment environment, IDeploymentSettingsManager settings, IAnalytics analytics)
            : base(traceFactory, environment, settings, analytics, Constants.ContinuousPath)
        {
            _jobsFileWatcher = new JobsFileWatcher(JobsBinariesPath, OnJobChanged, null, ListJobNames, traceFactory, analytics);
        }

        public override IEnumerable<ContinuousJob> ListJobs()
        {
            return ListJobsInternal();
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
                throw new JobNotFoundException();
            }

            ContinuousJobRunner continuousJobRunner;
            if (!_continuousJobRunners.TryGetValue(continuousJob.Name, out continuousJobRunner))
            {
                throw new InvalidOperationException("Missing job runner for an existing job - " + jobName);
            }

            continuousJobRunner.EnableJob();
        }

        private ContinuousJobRunner GetJobRunner(string jobName)
        {
            ContinuousJobRunner continuousJobRunner;
            if (!_continuousJobRunners.TryGetValue(jobName, out continuousJobRunner))
            {
                throw new JobNotFoundException();
            }
            return continuousJobRunner;
        }

        protected override void OnShutdown()
        {
            _jobsFileWatcher.Stop();

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
                    || !TryDelete(statusFile))
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

        private static bool TryDelete(string statusFile)
        {
            try
            {
                FileSystemHelpers.DeleteFileSafe(statusFile);
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
                RefreshJob(continuousJob, logRefresh: !_jobsFileWatcher.FirstTimeMakingChanges);
            }
        }

        private void RefreshJob(ContinuousJob continuousJob, bool logRefresh)
        {
            ContinuousJobRunner continuousJobRunner;
            if (!_continuousJobRunners.TryGetValue(continuousJob.Name, out continuousJobRunner))
            {
                continuousJobRunner = new ContinuousJobRunner(continuousJob, Environment, Settings, TraceFactory, Analytics);
                _continuousJobRunners.Add(continuousJob.Name, continuousJobRunner);
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

        private IEnumerable<string> ListJobNames()
        {
            IEnumerable<ContinuousJob> continuousJobs = ListJobs();
            return _continuousJobRunners.Keys.Union(continuousJobs.Select(j => j.Name));
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            // HACK: Next if statement should be removed once ninject wlll not dispose this class
            // Since ninject automatically calls dispose we currently disable it
            if (disposing)
            {
                return;
            }
            // End of code to be removed

            if (disposing)
            {
                if (_jobsFileWatcher != null)
                {
                    _jobsFileWatcher.Dispose();
                    _jobsFileWatcher = null;
                }
            }
        }
    }
}