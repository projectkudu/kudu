using System;
using System.Globalization;
using Kudu.Core.Jobs;
using Moq;
using Xunit;

namespace Kudu.Core.Test.Jobs
{
    public class ScheduleFacts
    {
        [Theory]
        [InlineData("10 * * * * *", "00:00:00 1/1/2015", "00:00:10", false)]
        [InlineData("0 1 * * * *", "00:00:00 1/1/2015", "00:01:00", false)]
        [InlineData("5 1 * * * *", "00:00:00 1/1/2015", "00:01:05", false)]
        [InlineData("0 0 2 * * *", "00:00:00 1/1/2015", "02:00:00", false)]
        [InlineData("* * * 3 * *", "00:00:00 1/1/2015", "2.00:00:00", false)]
        [InlineData("* * * * 2 *", "00:00:00 1/1/2015", "31.00:00:00", false)]
        [InlineData("* * * * 3 *", "00:00:00 1/1/2015", "59.00:00:00", false)]
        [InlineData("* * * * 4 *", "00:00:00 1/1/2015", "90.00:00:00", false)]
        [InlineData("10 * * * * *", "00:00:00 1/1/2014", "00:00:10", false)]
        [InlineData("0 */5 * * * *", "23:00:00 12/31/2014", "00:05:00", false)]
        [InlineData("0 */2 * * * *", "23:57:00 12/31/2014", "00:00:00", false)]
        [InlineData("0 */2 * * * *", "23:49:00 12/31/2014", "00:00:00", false)]
        [InlineData("0 */2 * * * *", "23:47:00 12/31/2014", "00:02:00", false)]
        [InlineData("0 */2 * * * *", "23:47:00 12/31/2014", "00:00:00", true)]
        [InlineData("10 * * * * *", "00:00:00 1/1/2015", "00:00:10", true)]
        public void ScheduleProvidesNextIntervalCorrectly(string cronExpression, string lastScheduleStr, string expectedNextIntervalStr, bool ignoreMissed)
        {
            var mockEnvironment = new Mock<IEnvironment>();
            mockEnvironment.Setup(p => p.JobsDataPath).Returns(String.Empty);
            var triggeredJobSchedulerLoggerMock = new Mock<TriggeredJobSchedulerLogger>(String.Empty , mockEnvironment.Object , null);

            // Running on the Friday before the DST switch at 2 AM on 3/11 (Pacific Standard Time)
            var schedule = Schedule.BuildSchedule(cronExpression , triggeredJobSchedulerLoggerMock.Object);
            var now = DateTime.Parse("00:00:00 1/1/2015" , CultureInfo.InvariantCulture , DateTimeStyles.AssumeLocal);
            schedule.SetDateTimeProvider(new TestDateTimeNowProvider(now));
            DateTime lastSchedule = DateTime.Parse(lastScheduleStr , CultureInfo.InvariantCulture , DateTimeStyles.AssumeLocal);
            TimeSpan actualNextSchedule = schedule.GetNextInterval(lastSchedule , ignoreMissed);
            TimeSpan expectedNextInterval = TimeSpan.Parse(expectedNextIntervalStr);
            expectedNextInterval = GetTimeZoneAdjustedInterval(expectedNextInterval , now);

            Assert.Equal(expectedNextInterval , actualNextSchedule);
        }

        /// <summary>
        /// Situation where the DST transition happens in the middle of the schedule, with the
        /// next occurrence AFTER the DST transition.
        /// </summary>
        [Fact]
        public void GetNextInterval_NextAfterDST_ReturnsExpectedValue()
        {
            var mockEnvironment = new Mock<IEnvironment>();
            mockEnvironment.Setup(p => p.JobsDataPath).Returns(String.Empty);
            var triggeredJobSchedulerLoggerMock = new Mock<TriggeredJobSchedulerLogger>(String.Empty , mockEnvironment.Object , null);

            // Running at 1:59 AM, i.e. one minute before the DST switch at 2 AM on 3/11 (Pacific Standard Time)
            var schedule = Schedule.BuildSchedule("0 0 18 * * 5" , triggeredJobSchedulerLoggerMock.Object);
            var now = new DateTime(2018 , 3 , 9 , 18 , 0 , 0 , DateTimeKind.Local);
            schedule.SetDateTimeProvider(new TestDateTimeNowProvider(now));
            TimeSpan interval = schedule.GetNextInterval(now , ignoreMissed: true);
            var expectedInterval = new TimeSpan(168 , 0 , 0);

            // One week is normally 168 hours, but it's 167 hours across DST
            expectedInterval = GetTimeZoneAdjustedInterval(expectedInterval , now);

            Assert.Equal(expectedInterval , interval);
        }

        /// <summary>
        /// Situation where the next occurrence falls within the hour that will be skipped
        /// as part of the DST transition (i.e. an invalid time).
        /// </summary>
        [Fact]
        public void GetNextInterval_NextWithinDST_ReturnsExpectedValue()
        {
            var mockEnvironment = new Mock<IEnvironment>();
            mockEnvironment.Setup(p => p.JobsDataPath).Returns(String.Empty);
            var triggeredJobSchedulerLoggerMock = new Mock<TriggeredJobSchedulerLogger>(String.Empty, mockEnvironment.Object, null);

            // Running at 1:59 AM, i.e. one minute before the DST switch at 2 AM on 3/11 (Pacific Standard Time)
            var schedule = Schedule.BuildSchedule("0 59 * * * *", triggeredJobSchedulerLoggerMock.Object);
            var now = new DateTime(2018, 3, 11, 1, 59, 0, DateTimeKind.Local);
            schedule.SetDateTimeProvider(new TestDateTimeNowProvider(now));

            // Note: NCronTab actually gives us an invalid next occurrence of 2:59 AM, which doesn't actually
            // exist because the DST switch skips from 2 to 3
            TimeSpan interval = schedule.GetNextInterval(now, ignoreMissed: true);

            Assert.Equal(1, interval.TotalHours);
        }

        private static TimeSpan GetTimeZoneAdjustedInterval(TimeSpan interval, DateTime now)
        {
            var nowTimeOffset = new DateTimeOffset(now).Offset;
            var expectedTimeOffset = new DateTimeOffset(now.Add(interval)).Offset;

            return interval.Add(nowTimeOffset - expectedTimeOffset);
        }

        private class TestDateTimeNowProvider : Schedule.IDateTimeNowProvider
        {
            public TestDateTimeNowProvider(DateTime now)
            {
                Now = now;
            }

            public DateTime Now { get; private set; }
        }
    }
}
