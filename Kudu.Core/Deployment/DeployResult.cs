using System;

namespace Kudu.Core.Deployment {
    public class DeployResult {
        public string Id { get; set; }
        public DeployStatus Status { get; set; }
        public string StatusText { get; set; }
        public DateTime DeployStartTime { get; set; }
        public DateTime? DeployEndTime { get; set; }
        public int Percentage { get; set; }
    }
}
