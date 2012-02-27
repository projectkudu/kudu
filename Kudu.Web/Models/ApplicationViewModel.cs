using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Kudu.Core.Deployment;
using Kudu.Core.SourceControl;

namespace Kudu.Web.Models
{
    public class ApplicationViewModel
    {
        public ApplicationViewModel()
        {
        }

        public ApplicationViewModel(Application application)
        {
            Name = application.Name;
            SiteUrl = application.SiteUrl;
            ServiceUrl = application.ServiceUrl;
            DeveloperSiteUrl = application.DeveloperSiteUrl;
            GitUrl = GetCloneUrl(application, RepositoryType.Git);
            HgUrl = GetCloneUrl(application, RepositoryType.Mercurial);
        }

        [Required]
        public string Name { get; set; }
        public string SiteUrl { get; set; }
        public string ServiceUrl { get; set; }
        public string DeveloperSiteUrl { get; set; }
        public RepositoryType RepositoryType { get; set; }
        public string GitUrl { get; set; }
        public string HgUrl { get; set; }
        public IList<DeployResult> Deployments { get; set; }

        public string CloneUrl
        {
            get
            {
                switch (RepositoryType)
                {
                    case RepositoryType.Git:
                        return GitUrl;
                    case RepositoryType.Mercurial:
                        return HgUrl;
                }
                return null;
            }
        }

        private string GetCloneUrl(Application application, RepositoryType type)
        {
            return application.ServiceUrl + (type == RepositoryType.Git ? "git" : "hg");
        }
    }
}