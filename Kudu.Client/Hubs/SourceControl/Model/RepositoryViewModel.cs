using System.Collections.Generic;

namespace Kudu.Client.Model {
    public class RepositoryViewModel {
        public string RepositoryType { get; set; }
        public Dictionary<string, IEnumerable<string>> Branches { get; set; }
        public string CloneUrl { get; set; }
        public Dictionary<string, DeployResultViewModel> Deployments { get; set; }
    }
}