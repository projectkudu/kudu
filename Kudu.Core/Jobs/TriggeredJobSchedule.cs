using System;
using System.Linq.Expressions;
using System.Threading;
using Kudu.Contracts.Jobs;
using Kudu.Core.Tracing;

namespace Kudu.Core.Jobs
{
    /// <summary>
    /// Responsible for scheduling of a triggered job.
    /// </summary>
    public class TriggeredJobSchedule : IDisposable
    {
        private readonly Action<TriggeredJobSchedule> _onSchedule;

        private Timer _timer;

        public TriggeredJobSchedule(TriggeredJob triggeredJob, Action<TriggeredJobSchedule> onSchedule, TriggeredJobSchedulerLogger logger)
        {
            TriggeredJob = triggeredJob;
            _onSchedule = onSchedule;
            Logger = logger;

            _timer = new Timer(OnTimer, triggeredJob, Timeout.Infinite, Timeout.Infinite);
        }

        public TriggeredJob TriggeredJob { get; private set; }

        public Schedule Schedule { get; private set; }

        public TriggeredJobSchedulerLogger Logger { get; private set; }

        public void Reschedule(DateTime lastRun, Schedule schedule = null)
        {
            Schedule = schedule ?? Schedule;

            var nextInterval = Schedule.GetNextInterval(lastRun);

            if (schedule != null)
            {
                Logger.LogInformation("Scheduling WebJob with " + schedule + " next schedule expected in " + nextInterval);
            }
            else
            {
                Logger.LogInformation("Next schedule expected in " + nextInterval);
            }

            _timer.Change(nextInterval, Timeout.InfiniteTimeSpan);
        }

        private void OnTimer(object state)
        {
            try
            {
                _onSchedule(this);
            }
            catch(Exception ex)
            {
                Logger.LogError(ex.ToString());
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_timer != null)
                {
                    _timer.Dispose();
                    _timer = null;
                }
            }
        }
    }
}
