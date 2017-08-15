using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Kudu.Contracts.Jobs;
using Kudu.Contracts.Settings;
using Kudu.Core.Hooks;
using Kudu.Core.Tracing;

namespace Kudu.Core.Jobs
{
    /// <summary>
    /// Responsible for scheduling the invocation of triggered jobs with the schedule setting on them
    /// </summary>
    public class TriggeredJobsScheduler
    {
        private readonly ITriggeredJobsManager _triggeredJobsManager;
        private readonly ITraceFactory _traceFactory;
        private readonly IEnvironment _environment;
        private readonly IDeploymentSettingsManager _settings;
        private readonly IAnalytics _analytics;

        private readonly Dictionary<string, TriggeredJobSchedule> _triggeredJobsSchedules = new Dictionary<string, TriggeredJobSchedule>(StringComparer.OrdinalIgnoreCase);

        public TriggeredJobsScheduler(ITriggeredJobsManager triggeredJobsManager, ITraceFactory traceFactory, IEnvironment environment, IDeploymentSettingsManager settings, IAnalytics analytics)
        {
            _triggeredJobsManager = triggeredJobsManager;
            _traceFactory = traceFactory;
            _environment = environment;
            _settings = settings;
            _analytics = analytics;

            _triggeredJobsManager.RegisterExtraEventHandlerForFileChange(OnJobChanged);
        }

        /// <summary>
        /// Process triggered job schedule when the settings.job file changed
        /// </summary>
        private void OnJobChanged(string jobName)
        {
            TriggeredJobSchedule triggeredJobSchedule;
            _triggeredJobsSchedules.TryGetValue(jobName, out triggeredJobSchedule);

            TriggeredJob triggeredJob = _triggeredJobsManager.GetJob(jobName);

            if (_settings.IsWebJobsScheduleDisabled())
            {
                _traceFactory.GetTracer().Trace("All WebJobs schedules have been disabled via WEBJOBS_DISABLE_SCHEDULE");
            }
            else if (triggeredJob != null)
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
                            triggeredJobSchedule = new TriggeredJobSchedule(triggeredJob, OnSchedule, logger, _analytics);
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
                triggeredJobSchedule.Logger.LogInformation("Removing current schedule from WebJob");

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
    }
}