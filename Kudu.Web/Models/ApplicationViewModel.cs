using System.ComponentModel.DataAnnotations;
using Kudu.Core.SourceControl;

namespace Kudu.Web.Models {
    public class ApplicationViewModel {
        public ApplicationViewModel() {
        }

        public ApplicationViewModel(Application app) {
            Id = app.Id;
            Name = app.Name;
            RepositoryType = (RepositoryType)app.RepositoryType;
            SiteUrl = app.SiteUrl;
        }

        public int Id { get; set; }
        [Required]
        public string Name { get; set; }
        public string SiteUrl { get; set; }
        public RepositoryType RepositoryType { get; set; }
    }
}