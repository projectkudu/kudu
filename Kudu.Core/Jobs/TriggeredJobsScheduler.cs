using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Kudu.Contracts.Jobs;
using Kudu.Core.Hooks;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;

namespace Kudu.Core.Jobs
{
    /// <summary>
    /// Responsible for scheduling the invocation of triggered jobs with the schedule setting on them
    /// </summary>
    public class TriggeredJobsScheduler : IDisposable
    {
        private readonly ITriggeredJobsManager _triggeredJobsManager;
        private readonly ITraceFactory _traceFactory;
        private readonly IEnvironment _environment;

        private readonly Dictionary<string, TriggeredJobSchedule> _triggeredJobsSchedules = new Dictionary<string, TriggeredJobSchedule>(StringComparer.OrdinalIgnoreCase);

        private JobsFileWatcher _jobsFileWatcher;

        public TriggeredJobsScheduler(ITriggeredJobsManager triggeredJobsManager, ITraceFactory traceFactory, IAnalytics analytics, IEnvironment environment)
        {
            _triggeredJobsManager = triggeredJobsManager;
            _traceFactory = traceFactory;
            _environment = environment;

            _jobsFileWatcher = new JobsFileWatcher(triggeredJobsManager.JobsBinariesPath, OnJobChanged, JobSettings.JobSettingsFileName, ListJobNames, traceFactory, analytics);
        }

        private IEnumerable<string> ListJobNames()
        {
            IEnumerable<TriggeredJob> triggeredJobs = _triggeredJobsManager.ListJobs();
            return triggeredJobs.Select(triggeredJob => triggeredJob.Name);
        }

        /// <summary>
        /// Process triggered job schedule when the settings.job file changed
        /// </summary>
        private void OnJobChanged(string jobName)
        {
            TriggeredJobSchedule triggeredJobSchedule;
            _triggeredJobsSchedules.TryGetValue(jobName, out triggeredJobSchedule);

            TriggeredJob triggeredJob = _triggeredJobsManager.GetJob(jobName);
            if (triggeredJob != null)
            {
                string cronExpression = triggeredJob.Settings != null ? triggeredJob.Settings.GetSchedule() : null;
                if (cronExpression != null)
                {
                    var logger = new TriggeredJobSchedulerLogger(triggeredJob.Name, _environment, _traceFactory);

                    Schedule schedule = Schedule.BuildSchedule(cronExpression, logger);
                    if (schedule != null)
                    {
                        if (triggeredJobSchedule == null)
                        {
                            triggeredJobSchedule = new TriggeredJobSchedule(triggeredJob, OnSchedule, logger);
                            _triggeredJobsSchedules[jobName] = triggeredJobSchedule;
                        }

                        DateTime lastRun = triggeredJob.LatestRun != null
                            ? triggeredJob.LatestRun.StartTime
                            : DateTime.MinValue; // DateTIme.Min if triggered job was never run.

                        triggeredJobSchedule.Reschedule(lastRun, schedule);

                        return;
                    }
                }
            }

            if (triggeredJobSchedule != null)
            {
                _traceFactory.GetTracer().Trace("Removing schedule for triggered WebJob {0}".FormatCurrentCulture(jobName));

                triggeredJobSchedule.Dispose();
                _triggeredJobsSchedules.Remove(jobName);
            }
        }

        private void OnSchedule(TriggeredJobSchedule triggeredJobSchedule)
        {
            bool invoked = false;
            try
            {
                string triggeredJobName = triggeredJobSchedule.TriggeredJob.Name;

                TriggeredJobRun latestTriggeredJobRun = _triggeredJobsManager.GetLatestJobRun(triggeredJobName);
                DateTime lastRun = latestTriggeredJobRun != null ? latestTriggeredJobRun.StartTime : DateTime.Now.AddMinutes(-1);

                // Make sure we are on schedule
                // Check for the next occurence after the last run (as of now)
                // If it is still now, invoke the triggered job
                // If it's not now (in the future) reschedule the triggered job schedule starting with the last triggered job run
                TimeSpan currentSchedule = triggeredJobSchedule.Schedule.GetNextInterval(lastRun, ignoreMissed: true);
                if (currentSchedule == TimeSpan.Zero)
                {
                    _triggeredJobsManager.InvokeTriggeredJob(triggeredJobName, null, "Schedule - " + triggeredJobSchedule.Schedule);
                    invoked = true;
                }
                else
                {
                    triggeredJobSchedule.Reschedule(lastRun);
                    return;
                }
            }
            catch (ConflictException)
            {
                // Ignore as this is expected when running multiple instances
            }
            catch (Exception ex)
            {
                _traceFactory.GetTracer().TraceError(ex);
            }

            if (invoked)
            {
                triggeredJobSchedule.Logger.LogInformation("WebJob invoked");
            }

            triggeredJobSchedule.Reschedule(DateTime.Now);
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