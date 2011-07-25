using System.ComponentModel.DataAnnotations;

namespace Kudu.Web.Models {
    public class Application {
        [Key]
        public string Name { get; set; }
        public string Slug { get; set; }
        public string SiteName { get; set; }
        public string ServiceAppName { get; set; }
        public string ServiceUrl { get; set; }
        public string SiteUrl { get; set; }
        public int RepositoryType { get; set; }
    }
}