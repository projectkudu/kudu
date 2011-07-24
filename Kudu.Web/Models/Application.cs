using System.ComponentModel.DataAnnotations;

namespace Kudu.Web.Models {
    public class Application {
        public int Id { get; set; }
        public string Name { get; set; }
        public string SiteName { get; set; }
        public string ServiceUrl { get; set; }
        public string SiteUrl { get; set; }
        public int RepositoryType { get; set; }
    }
}