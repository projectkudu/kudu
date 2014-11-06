using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Kudu.Core.Deployment;
using Kudu.Core.SourceControl;
using Kudu.SiteManagement;
using Kudu.SiteManagement.Configuration;

namespace Kudu.Web.Models
{
    public class ApplicationViewModel
    {
        public ApplicationViewModel()
        {
        }

        public ApplicationViewModel(IApplication application, IKuduConfiguration configuration)
        {
            Name = application.Name;
            SiteUrl = application.SiteUrl;
            SiteUrls = application.SiteUrls;
            ServiceUrl = application.ServiceUrl;
            ServiceUrls = application.ServiceUrls;

            CustomHostNames = configuration.CustomHostNamesEnabled;
        }

        [Required]
        public string Name { get; set; }
        public string SiteUrl { get; set; }
        public IEnumerable<string> SiteUrls { get; set; }
        public string ServiceUrl { get; set; }
        public IEnumerable<string> ServiceUrls { get; set; }
        public bool CustomHostNames { get; private set; }
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