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
        }

        [Required]
        public string Name { get; set; }
        public string SiteUrl { get; set; }
        public string ServiceUrl { get; set; }
        public string DeveloperSiteUrl { get; set; }
        public RepositoryInfo RepositoryInfo { get; set; }
        
        public string GitUrl
        {
            get
            {
                if (RepositoryInfo == null)
                {
                    return null;
                }
                return RepositoryInfo.GitUrl.ToString();
            }
        }

        public IList<DeployResult> Deployments { get; set; }

        public string CloneUrl
        {
            get
            {
                if (RepositoryInfo == null)
                {
                    return null;
                }

                switch (RepositoryInfo.Type)
                {
                    case RepositoryType.Git:
                        return GitUrl;
                }
                return null;
            }
        }
    }
}