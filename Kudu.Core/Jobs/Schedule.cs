using System;
using System.Collections.Generic;
using System.Linq;
using Kudu.Core.Tracing;
using NCrontab;

namespace Kudu.Core.Jobs
{
    public class Schedule
    {
        private readonly CrontabSchedule _crontabSchedule;
        private readonly TriggeredJobSchedulerLogger _logger;
        private IDateTimeNowProvider _dateTimeNowProvider = DateTimeNowProvider.Instance;

        private Schedule(CrontabSchedule crontabSchedule, TriggeredJobSchedulerLogger logger)
        {
            _crontabSchedule = crontabSchedule;
            _logger = logger;
        }

        public static Schedule BuildSchedule(string cronExpression, TriggeredJobSchedulerLogger logger)
        {
            try
            {
                var crontabSchedule = CrontabSchedule.Parse(cronExpression, new CrontabSchedule.ParseOptions() {IncludingSeconds = true});
                return crontabSchedule != null ? new Schedule(crontabSchedule, logger) : null;
            }
            catch (Exception ex)
            {
                logger.LogError("Failed to parse schedule \"{0}\". Error: {1}".FormatCurrentCulture(cronExpression, ex.Message));
                return null;
            }
        }

        public TimeSpan GetNextInterval(DateTime lastSchedule, bool ignoreMissed = false)
        {
            DateTime now = _dateTimeNowProvider.Now;

            lastSchedule = lastSchedule == DateTime.MinValue ? now : lastSchedule.ToLocalTime();

            // Check for next occurrence from last occurrence
            DateTime nextOccurrence = _crontabSchedule.GetNextOccurrence(lastSchedule);

            // Note: For calculations, we use DateTimeOffsets and TimeZoneInfo to ensure we honor time zone
            // changes (e.g. Daylight Savings Time)
            var nowOffset = new DateTimeOffset(now, TimeZoneInfo.Local.GetUtcOffset(now));
            var nextOffset = new DateTimeOffset(nextOccurrence, TimeZoneInfo.Local.GetUtcOffset(nextOccurrence));
            if (nextOffset >= nowOffset)
            {
                // If next occurrence is in the future use it
                return nextOffset - nowOffset;
            }

            // Otherwise if next occurrence is up to 10 minutes in the past or ignore missed is true use now
            if (ignoreMissed || nextOccurrence >= now - TimeSpan.FromMinutes(10))
            {
                return TimeSpan.Zero;
            }

            var nextOccurrences = _crontabSchedule.GetNextOccurrences(lastSchedule, now);
            int nextOccurrencesCount = nextOccurrences.Count();
            if (nextOccurrencesCount > 0)
            {
                _logger.LogWarning("Missed {0} schedules, most recent at {1}".FormatCurrentCulture(nextOccurrencesCount, nextOccurrences.Last()));
            }

            // Return next occurrence after now
            nextOccurrence = _crontabSchedule.GetNextOccurrence(now);
            nextOffset = new DateTimeOffset(nextOccurrence, TimeZoneInfo.Local.GetUtcOffset(nextOccurrence));
            return nextOffset - nowOffset;
        }

        public override string ToString()
        {
            return _crontabSchedule.ToString();
        }

        internal void SetDateTimeProvider(IDateTimeNowProvider dateTimeNowProvider)
        {
            _dateTimeNowProvider = dateTimeNowProvider;
        }

        internal interface IDateTimeNowProvider
        {
            DateTime Now { get; }
        }

        internal class DateTimeNowProvider : IDateTimeNowProvider
        {
            public static readonly DateTimeNowProvider Instance = new DateTimeNowProvider();

            public DateTime Now
            {
                get { return DateTime.Now; }
            }
        }
    }
}
