using System;

namespace Kudu.Core.Deployment {
    public class DeployResult {
        public string Id { get; set; }
        public bool Failed { get; set; }
        public string Log { get; set; }
        public DateTime? DeployTime { get; set; }
    }
}
