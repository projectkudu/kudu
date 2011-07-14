using System.Collections.Generic;

namespace Kudu.Web.Model {
    public class RepositoryViewModel {
        public string RepositoryType { get; set; }
        public Dictionary<string, IEnumerable<string>> Branches { get; set; }
    }
}