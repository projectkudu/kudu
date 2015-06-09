using System;

namespace Kudu.Core.Jobs
{
    public class TriggeredJobStatus : IJobStatus
    {
        public string Trigger { get; set; }
        public string Status { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
    }
}