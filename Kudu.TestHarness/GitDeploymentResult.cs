using System;

namespace Kudu.TestHarness
{
    public class GitDeploymentResult
    {
        public TimeSpan PushResponseTime { get; set; }
        public TimeSpan TotalResponseTime { get; set; }
        public bool TimedOut { get; set; }
    }

}
