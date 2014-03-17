using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using Kudu.Contracts.Jobs;
using Kudu.Contracts.Settings;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;

namespace Kudu.Core.Jobs
{
    public class TriggeredJobsManager : JobsManagerBase<TriggeredJob>, ITriggeredJobsManager
    {
        private readonly ConcurrentDictionary<string, TriggeredJobRunner> _triggeredJobRunners =
            new ConcurrentDictionary<string, TriggeredJobRunner>(StringComparer.OrdinalIgnoreCase);

        public TriggeredJobsManager(ITraceFactory traceFactory, IEnvironment environment, IDeploymentSettingsManager settings, IAnalytics analytics)
            : base(traceFactory, environment, settings, analytics, Constants.TriggeredPath)
        {
        }

        public override IEnumerable<TriggeredJob> ListJobs()
        {
            return ListJobsInternal();
        }

        public override TriggeredJob GetJob(string jobName)
        {
            return GetJobInternal(jobName);
        }

        public TriggeredJobHistory GetJobHistory(string jobName, string etag, out string currentETag)
        {
            currentETag = null;
            var triggeredJobRuns = new List<TriggeredJobRun>();

            DirectoryInfoBase[] jobRunsDirectories = GetJobRunsDirectories(jobName);
            if (jobRunsDirectories == null)
            {
                return null;
            }

            bool isLatest = true;

            // Order runs by name (date) descending
            foreach (DirectoryInfoBase jobRunDirectory in jobRunsDirectories.OrderByDescending(j => j.Name))
            {
                TriggeredJobRun triggeredJobRun = BuildJobRun(jobRunDirectory, jobName, isLatest);
                if (triggeredJobRun != null)
                {
                    if (isLatest)
                    {
                        // The history state is determined by the most recent invocation,
                        // as previous ones are immutable (beind historical records).
                        currentETag = CalculateETag(triggeredJobRun);
                        if (currentETag == etag)
                        {
                            return null;
                        }
                    }
                    triggeredJobRuns.Add(triggeredJobRun);
                    isLatest = false;
                }
            }

            if (triggeredJobRuns.Count == 0)
            {
                currentETag = string.Format(CultureInfo.CurrentCulture, "\"{0:x}-{1:x}\"",
                    jobName.GetHashCode(), "EMPTY".GetHashCode());
            }

            return new TriggeredJobHistory { TriggeredJobRuns = triggeredJobRuns };
        }

        private static string CalculateETag(TriggeredJobRun triggeredJobRun)
        {
            // during a job's life time, the status and endtime could change, so
            // a job run state is made of its id, status, and end time.
            return string.Format(CultureInfo.CurrentCulture, "\"{0:x}-{1:x}-{2:x}\"",
                triggeredJobRun.Id.GetHashCode(),
                triggeredJobRun.Status.GetHashCode(),
                triggeredJobRun.EndTime.Ticks.GetHashCode());
        }

        public TriggeredJobRun GetJobRun(string jobName, string runId)
        {
            string triggeredJobRunPath = Path.Combine(JobsDataPath, jobName, runId);
            DirectoryInfoBase triggeredJobRunDirectory = FileSystemHelpers.DirectoryInfoFromDirectoryName(triggeredJobRunPath);

            return BuildJobRun(triggeredJobRunDirectory, jobName, isLatest: true);
        }

        protected override void UpdateJob(TriggeredJob job)
        {
            job.HistoryUrl = BuildJobsUrl(job.Name + "/history");
            job.LatestRun = BuildLatestJobRun(job.Name);
        }

        private TriggeredJobRun BuildLatestJobRun(string jobName)
        {
            DirectoryInfoBase[] jobRunsDirectories = GetJobRunsDirectories(jobName);
            if (jobRunsDirectories == null || jobRunsDirectories.Length == 0)
            {
                return null;
            }

            DirectoryInfoBase latestJobRunDirectory = jobRunsDirectories.OrderByDescending(j => j.Name).First();

            return BuildJobRun(latestJobRunDirectory, jobName, isLatest: true);
        }

        private DirectoryInfoBase[] GetJobRunsDirectories(string jobName)
        {
            string jobHistoryPath = Path.Combine(JobsDataPath, jobName);
            DirectoryInfoBase jobHistoryDirectory = FileSystemHelpers.DirectoryInfoFromDirectoryName(jobHistoryPath);
            if (!jobHistoryDirectory.Exists)
            {
                return null;
            }

            return jobHistoryDirectory.GetDirectories("*", SearchOption.TopDirectoryOnly);
        }

        private TriggeredJobRun BuildJobRun(DirectoryInfoBase jobRunDirectory, string jobName, bool isLatest)
        {
            if (!jobRunDirectory.Exists)
            {
                return null;
            }

            string runId = jobRunDirectory.Name;
            string triggeredJobRunPath = jobRunDirectory.FullName;
            string statusFilePath = Path.Combine(triggeredJobRunPath, TriggeredJobRunLogger.TriggeredStatusFile);

            var triggeredJobStatus = GetStatus<TriggeredJobStatus>(statusFilePath);

            if (triggeredJobStatus.Status == JobStatus.Running)
            {
                if (isLatest)
                {
                    // If it is the latest run, make sure it's actually running
                    string triggeredJobDataPath = Path.Combine(JobsDataPath, jobName);
                    LockFile triggeredJobRunLockFile = TriggeredJobRunner.BuildTriggeredJobRunnerLockFile(triggeredJobDataPath, TraceFactory);
                    if (!triggeredJobRunLockFile.IsHeld)
                    {
                        triggeredJobStatus.Status = JobStatus.Aborted;
                    }
                }
                else
                {
                    // If it's not latest run it cannot be running
                    triggeredJobStatus.Status = JobStatus.Aborted;
                }
            }

            return new TriggeredJobRun()
            {
                Id = runId,
                Status = triggeredJobStatus.Status,
                StartTime = triggeredJobStatus.StartTime,
                EndTime = triggeredJobStatus.EndTime,
                Url = BuildJobsUrl("{0}/history/{1}".FormatInvariant(jobName, runId)),
                OutputUrl = BuildVfsLogUrl(triggeredJobRunPath, jobName, runId, TriggeredJobRunLogger.OutputLogFileName),
                ErrorUrl = BuildVfsLogUrl(triggeredJobRunPath, jobName, runId, TriggeredJobRunLogger.ErrorLogFileName)
            };
        }

        private Uri BuildVfsLogUrl(string triggeredJobRunPath, string jobName, string runId, string fileName)
        {
            string filePath = Path.Combine(triggeredJobRunPath, fileName);

            if (FileSystemHelpers.FileExists(filePath))
            {
                return BuildVfsUrl("{0}/{1}/{2}".FormatInvariant(jobName, runId, fileName));
            }

            return null;
        }

        public void InvokeTriggeredJob(string jobName)
        {
            TriggeredJob triggeredJob = GetJob(jobName);
            if (triggeredJob == null)
            {
                throw new JobNotFoundException();
            }

            TriggeredJobRunner triggeredJobRunner =
                _triggeredJobRunners.GetOrAdd(
                    jobName,
                    _ => new TriggeredJobRunner(triggeredJob.Name, Environment, Settings, TraceFactory, Analytics));

            triggeredJobRunner.StartJobRun(triggeredJob);
        }
    }
}
