using System.Collections.Generic;
using Kudu.Web.Hubs.SourceControl.Model;

namespace Kudu.Web.Model {
    public class RepositoryViewModel {
        public string RepositoryType { get; set; }
        public Dictionary<string, IEnumerable<string>> Branches { get; set; }
        public string CloneUrl { get; set; }
        public Dictionary<string, DeployResultViewModel> Deployments { get; set; }
    }
}