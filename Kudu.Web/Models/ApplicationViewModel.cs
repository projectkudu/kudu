using System.ComponentModel.DataAnnotations;
using Kudu.Core.SourceControl;

namespace Kudu.Web.Models {
    public class ApplicationViewModel {
        public ApplicationViewModel() {
        }

        public ApplicationViewModel(Application app) {
            Name = app.Name;
            RepositoryType = (RepositoryType)app.RepositoryType;
            SiteUrl = app.SiteUrl;
            Slug = app.Slug;
        }

        [Required]
        public string Name { get; set; }
        public string SiteUrl { get; set; }
        public RepositoryType RepositoryType { get; set; }
        public string Slug { get; set; }
    }
}