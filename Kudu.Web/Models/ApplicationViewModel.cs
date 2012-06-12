using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Kudu.Core.Deployment;
using Kudu.Core.SourceControl;
using Kudu.SiteManagement;

namespace Kudu.Web.Models
{
    public class ApplicationViewModel
    {
        public ApplicationViewModel()
        {
        }

        public ApplicationViewModel(string name, Site site)
        {
            Name = name;
            SiteUrl = site.SiteUrl;
            ServiceUrl = site.ServiceUrl;
            DevSiteUrl = site.DevSiteUrl;
        }

        [Required]
        public string Name { get; set; }
        public string Hostname { get; set; }
        public int Port { get; set; }
        public string SiteUrl { get; set; }
        public string ServiceUrl { get; set; }
        public string DevSiteUrl { get; set; }

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