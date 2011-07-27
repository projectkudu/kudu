using System.Collections.Generic;
using Kudu.Core.Deployment;

namespace Kudu.Web.Model {
    public class RepositoryViewModel {
        public string RepositoryType { get; set; }
        public Dictionary<string, IEnumerable<string>> Branches { get; set; }
        public string CloneUrl { get; set; }
        public Dictionary<string, DeployResult> Deployments { get; set; }
    }
}