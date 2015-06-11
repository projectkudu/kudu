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

            var triggeredJobSchedulerLoggerMock = new Mock<TriggeredJobSchedulerLogger>(String.Empty, mockEnvironment.Object, null);
            var schedule = Schedule.BuildSchedule(cronExpression, triggeredJobSchedulerLoggerMock.Object);
            schedule.SetDateTimeProvider(new TestDateTimeNowProvider(DateTime.Parse("00:00:00 1/1/2015", CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal)));

            DateTime lastSchedule = DateTime.Parse(lastScheduleStr, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal);
            TimeSpan actualNextSchedule = schedule.GetNextInterval(lastSchedule, ignoreMissed);

            TimeSpan expectedNextInterval = TimeSpan.Parse(expectedNextIntervalStr);
            Assert.Equal(expectedNextInterval, actualNextSchedule);
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
