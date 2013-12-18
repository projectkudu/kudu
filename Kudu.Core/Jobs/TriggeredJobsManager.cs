using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using Kudu.Contracts.Jobs;
using Kudu.Contracts.Settings;
using Kudu.Core.Tracing;

namespace Kudu.Core.Jobs
{
    public class TriggeredJobsManager : JobsManagerBase<TriggeredJob>, ITriggeredJobsManager
    {
        private readonly ConcurrentDictionary<string, TriggeredJobRunner> _triggeredJobRunners =
            new ConcurrentDictionary<string, TriggeredJobRunner>(StringComparer.OrdinalIgnoreCase);

        private string _extraInfoUrlPrefix;

        public TriggeredJobsManager(ITraceFactory traceFactory, IEnvironment environment, IFileSystem fileSystem, IDeploymentSettingsManager settings, IAnalytics analytics)
            : base(traceFactory, environment, fileSystem, settings, analytics, Constants.TriggeredPath)
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

        public TriggeredJobHistory GetJobHistory(string jobName)
        {
            var triggeredJobRuns = new List<TriggeredJobRun>();
            var triggeredJobHistory = new TriggeredJobHistory()
            {
                TriggeredJobRuns = triggeredJobRuns
            };

            DirectoryInfoBase[] jobRunsDirectories = GetJobRunsDirectories(jobName);
            if (jobRunsDirectories == null)
            {
                return triggeredJobHistory;
            }

            foreach (DirectoryInfoBase jobRunDirectory in jobRunsDirectories)
            {
                TriggeredJobRun triggeredJobRun = BuildJobRun(jobRunDirectory, jobName);
                if (triggeredJobRun != null)
                {
                    triggeredJobRuns.Add(triggeredJobRun);
                }
            }

            return triggeredJobHistory;
        }

        public TriggeredJobRun GetJobRun(string jobName, string runId)
        {
            string triggeredJobRunPath = Path.Combine(JobsDataPath, jobName, runId);
            DirectoryInfoBase triggeredJobRunDirectory = FileSystem.DirectoryInfo.FromDirectoryName(triggeredJobRunPath);

            return BuildJobRun(triggeredJobRunDirectory, jobName);
        }

        protected override void UpdateJob(TriggeredJob job)
        {
            job.HistoryUrl = BuildJobsUrl(job.Name + "/history");
            job.LatestRun = BuildLatestJobRun(job.Name);
        }

        protected override Uri BuildDefaultExtraInfoUrl(string jobName)
        {
            if (_extraInfoUrlPrefix == null)
            {
                if (AppBaseUrlPrefix == null)
                {
                    return null;
                }

                _extraInfoUrlPrefix = AppBaseUrlPrefix + "/JobRuns/history.html";
            }

            return new Uri(_extraInfoUrlPrefix + "?jobName=" + jobName);
        }

        private TriggeredJobRun BuildLatestJobRun(string jobName)
        {
            DirectoryInfoBase[] jobRunsDirectories = GetJobRunsDirectories(jobName);
            if (jobRunsDirectories == null || jobRunsDirectories.Length == 0)
            {
                return null;
            }

            DirectoryInfoBase latestJobRunDirectory = jobRunsDirectories.OrderByDescending(j => j.Name).First();

            return BuildJobRun(latestJobRunDirectory, jobName);
        }

        private DirectoryInfoBase[] GetJobRunsDirectories(string jobName)
        {
            string jobHistoryPath = Path.Combine(JobsDataPath, jobName);
            DirectoryInfoBase jobHistoryDirectory = FileSystem.DirectoryInfo.FromDirectoryName(jobHistoryPath);
            if (!jobHistoryDirectory.Exists)
            {
                return null;
            }

            return jobHistoryDirectory.GetDirectories("*", SearchOption.TopDirectoryOnly);
        }

        private TriggeredJobRun BuildJobRun(DirectoryInfoBase jobRunDirectory, string jobName)
        {
            if (!jobRunDirectory.Exists)
            {
                return null;
            }

            string runId = jobRunDirectory.Name;
            string triggeredJobRunPath = jobRunDirectory.FullName;
            string statusFilePath = Path.Combine(triggeredJobRunPath, TriggeredJobRunLogger.TriggeredStatusFile);

            var triggeredJobStatus = GetStatus<TriggeredJobStatus>(statusFilePath);

            return new TriggeredJobRun()
            {
                Id = runId,
                Status = triggeredJobStatus.Status,
                StartTime = triggeredJobStatus.StartTime,
                EndTime = triggeredJobStatus.EndTime,
                Url = BuildJobsUrl("{0}/history/{1}".FormatInvariant(jobName, runId)),
                OutputUrl = BuildVfsLogUrl(triggeredJobRunPath, jobName, runId, "output.log"),
                ErrorUrl = BuildVfsLogUrl(triggeredJobRunPath, jobName, runId, "error.log")
            };
        }

        private Uri BuildVfsLogUrl(string triggeredJobRunPath, string jobName, string runId, string fileName)
        {
            string filePath = Path.Combine(triggeredJobRunPath, fileName);

            if (FileSystem.File.Exists(filePath))
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
                    _ => new TriggeredJobRunner(triggeredJob.Name, Environment, FileSystem, Settings, TraceFactory, Analytics));

            triggeredJobRunner.StartJobRun(triggeredJob);
        }
    }
}