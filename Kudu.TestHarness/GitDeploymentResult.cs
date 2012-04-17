using System;

namespace Kudu.TestHarness
{
    public class GitDeploymentResult
    {
        public TimeSpan TotalResponseTime { get; set; }
        public string GitTrace { get; set; }
    }

}
