using System;
using System.ComponentModel.DataAnnotations;
using Kudu.Client.Models;
using Kudu.Core.SourceControl;
using Kudu.Web.Infrastructure;

namespace Kudu.Web.Models {
    public class ApplicationViewModel {
        public ApplicationViewModel() {
        }

        public ApplicationViewModel(IApplication application) {
            Name = application.Name;
            SiteUrl = application.SiteUrl;
            DeveloperSiteUrl = application.DeveloperSiteUrl;
            GitUrl = GetCloneUrl(application, RepositoryType.Git);
            HgUrl = GetCloneUrl(application, RepositoryType.Mercurial);
        }

        [Required]
        public string Name { get; set; }
        public string SiteUrl { get; set; }
        public string DeveloperSiteUrl { get; set; }
        public RepositoryType RepositoryType { get; set; }
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

        private string GetCloneUrl(IApplication application, RepositoryType type) {
            string prefix = application.ServiceUrl + application.Name.GenerateSlug();
            return prefix + (type == RepositoryType.Git ? ".git" : String.Empty);
        }
    }
}