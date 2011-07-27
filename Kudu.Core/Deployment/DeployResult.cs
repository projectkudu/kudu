using System;

namespace Kudu.Core.Deployment {
    public class DeployResult {
        public string Id { get; set; }
        public DeployStatus Status { get; set; }
        public string StatusText { get; set; }
        public DateTime? DeployTime { get; set; }
        public int Percentage { get; set; }
    }
}
