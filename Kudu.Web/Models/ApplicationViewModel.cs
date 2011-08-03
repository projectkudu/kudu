using System;
using System.ComponentModel.DataAnnotations;
using Kudu.Core.SourceControl;

namespace Kudu.Web.Models {
    public class ApplicationViewModel {
        public ApplicationViewModel() {
        }

        public ApplicationViewModel(Application app) {
            Name = app.Name;
            SiteUrl = app.SiteUrl;
            Slug = app.Slug;
            GitUrl = GetCloneUrl(app, RepositoryType.Git);
            HgUrl = GetCloneUrl(app, RepositoryType.Mercurial);
        }

        [Required]
        public string Name { get; set; }
        public string SiteUrl { get; set; }
        public RepositoryType RepositoryType { get; set; }
        public string Slug { get; set; }
        public string GitUrl { get; set; }
        public string HgUrl { get; set; }
        public string CloneUrl {
            get {
                switch (RepositoryType) {
                    case RepositoryType.Git:
                        return GitUrl;
                    case RepositoryType.Mercurial:
                        return HgUrl;
                }
                return null;
            }
        }

        private string GetCloneUrl(Application application, RepositoryType type) {
            string prefix = application.ServiceUrl + application.Slug;
            return prefix + (type == RepositoryType.Git ? ".git" : String.Empty);
        }
    }
}