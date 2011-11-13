using System;
using System.Diagnostics;

namespace Kudu.Core.Deployment
{
    [DebuggerDisplay("{Id} {Status}")]
    public class DeployResult
    {
        public string Id { get; set; }
        public DeployStatus Status { get; set; }
        public string StatusText { get; set; }
        public string Author { get; set; }
        public string Message { get; set; }
        public DateTime DeployStartTime { get; set; }
        public DateTime? DeployEndTime { get; set; }
        public int Percentage { get; set; }
    }
}
