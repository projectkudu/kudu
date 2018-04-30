using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kudu.Contracts.Jobs;
using Kudu.Contracts.Settings;
using Kudu.Core.Tracing;

namespace Kudu.Core.Jobs
{
    public abstract class AggregateJobsManagerBase<TJob> where TJob : JobBase, new()
    {
        protected JobsManagerBase<TJob> PrimaryJobManager { get; private set; }
        protected JobsManagerBase<TJob> SecondaryJobManager { get; private set; }
        private readonly IDeploymentSettingsManager _settings;
        private readonly IEnvironment _environment;
        private readonly ITraceFactory _traceFactory;
        private readonly string _jobType;

        protected AggregateJobsManagerBase(JobsManagerBase<TJob> primaryManager, Func<IEnumerable<string>, JobsManagerBase<TJob>> secondaryManagerFactory, IDeploymentSettingsManager settings, IEnvironment environment, ITraceFactory traceFactory, string jobType)
        {
            PrimaryJobManager = primaryManager;
            // pass the list of primary job names so the second manager can excluded them
            SecondaryJobManager = secondaryManagerFactory(PrimaryJobManager.ListJobs(forceRefreshCache: false).Select(j => j.Name));
            _settings = settings;
            _environment = environment;
            _traceFactory = traceFactory;
            _jobType = jobType;
        }

        public void DeleteJob(string jobName)
        {
            if (_settings.RunFromZip())
            {
                SecondaryJobManager.DeleteJob(jobName);
            }
            else
            {
                // Make sure to delete the job from both managers if it exists.
                // Calling DeleteJob on a non-existing job is a no-op.
                PrimaryJobManager.DeleteJob(jobName);
                SecondaryJobManager.DeleteJob(jobName);
            }
        }

        public void CleanupDeletedJobs()
        {
            var jobs = ListJobs(forceRefreshCache: true).Select(j => j.Name);
            var jobsDataPath = Path.Combine(_environment.JobsDataPath, _jobType);
            JobsManagerBase.CleanupDeletedJobs(jobs, jobsDataPath, _traceFactory.GetTracer());
        }

        public TJob CreateOrReplaceJobFromFileStream(Stream scriptFileStream, string jobName, string scriptFileName)
            => GetWriteJobManagerForJob(jobName).CreateOrReplaceJobFromFileStream(scriptFileStream, jobName, scriptFileName);

        public TJob CreateOrReplaceJobFromZipStream(Stream zipStream, string jobName)
            => GetWriteJobManagerForJob(jobName).CreateOrReplaceJobFromZipStream(zipStream, jobName);

        public TJob GetJob(string jobName) 
            => PrimaryJobManager.HasJob(jobName) ? PrimaryJobManager.GetJob(jobName) : SecondaryJobManager.GetJob(jobName);

        public JobSettings GetJobSettings(string jobName)
            => PrimaryJobManager.HasJob(jobName) ? PrimaryJobManager.GetJobSettings(jobName) : SecondaryJobManager.GetJobSettings(jobName);

        public bool HasJob(string jobName)
            => PrimaryJobManager.HasJob(jobName) || SecondaryJobManager.HasJob(jobName);

        public IEnumerable<TJob> ListJobs(bool forceRefreshCache)
            => PrimaryJobManager.ListJobs(forceRefreshCache).Concat(SecondaryJobManager.ListJobs(forceRefreshCache));

        public void RegisterExtraEventHandlerForFileChange(Action<string> action)
        {
            PrimaryJobManager.RegisterExtraEventHandlerForFileChange(action);
            SecondaryJobManager.RegisterExtraEventHandlerForFileChange(action);
        }

        public void SetJobSettings(string jobName, JobSettings jobSettings)
            => GetWriteJobManagerForJob(jobName).SetJobSettings(jobName, jobSettings);

        // Always sync webjobs brought in by site extensions
        // into the writable location.
        public void SyncExternalJobs(string sourcePath, string sourceName)
            => WritableJobManager.SyncExternalJobs(sourcePath, sourceName);

        // Always sync webjobs brought in by site extensions
        // into the writable location.
        public void CleanupExternalJobs(string sourceName)
            => WritableJobManager.CleanupExternalJobs(sourceName);

        // Writable manager is secondary if run from zip, and primary otherwise.
        private JobsManagerBase<TJob> WritableJobManager => _settings.RunFromZip()
            ? SecondaryJobManager
            : PrimaryJobManager;

        // This checks both run from zip, and Primary.HasJob()
        // The point is that if we are in run from zip, we always use the secondary.
        // Otherwise, we use the manager where the job exists. This can be either the primary or the secondary.
        protected JobsManagerBase<TJob> GetWriteJobManagerForJob(string jobName)
        {
            if (_settings.RunFromZip())
            {
                return SecondaryJobManager;
            }
            else if (PrimaryJobManager.HasJob(jobName))
            {
                return PrimaryJobManager;
            }
            else if (SecondaryJobManager.HasJob(jobName))
            {
                return SecondaryJobManager;
            }
            else
            {
                return PrimaryJobManager;
            }
        }
    }
}