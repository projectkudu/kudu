using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web.Mvc;
using Kudu.Core.Deployment;
using Kudu.Core.SourceControl;
using Kudu.SiteManagement;
using Kudu.SiteManagement.Certificates;
using Kudu.SiteManagement.Configuration;
using Kudu.SiteManagement.Context;

namespace Kudu.Web.Models
{
    public class ApplicationViewModel
    {
        public ApplicationViewModel()
        {
            Schemas = new[]
            {
                new SelectListItem {Text = "Http://", Value = "Http://", Selected = true},
                new SelectListItem {Text = "Https://", Value = "Https://"}
            };
        }

        public ApplicationViewModel(IApplication application, IKuduContext context,
            IEnumerable<Certificate> certificates)
            : this()
        {
            //Certificates = certificates;
            Name = application.Name;
            PrimarySiteBinding = application.PrimarySiteBinding;
            SiteBindings = application.SiteBindings;
            PrimaryServiceBinding = application.PrimaryServiceBinding;
            ServiceBindings = application.ServiceBindings;

            CustomHostNames = context.Configuration.CustomHostNamesEnabled;

            Certificates = certificates
                .Select(cert => new SelectListItem { Text = cert.FriendlyName, Value = cert.Thumbprint })
                .ToArray();

            IpAddresses = (new[] { new SelectListItem {Text = "All Unassigned", Value = "*", Selected = true } })
                .Union(context.IPAddresses.Select(ip => new SelectListItem { Text = ip, Value = ip }))
                .ToArray();

            SupportsSni = context.IISVersion.Major == 8;
        }

        [Required]
        public string Name { get; set; }
        public string PrimarySiteUrl { get { return PrimarySiteBinding.ToString("T"); } }
        public KuduBinding PrimarySiteBinding { get; set; }
        public IEnumerable<KuduBinding> SiteBindings { get; set; }
        public string PrimaryServiceUrl { get { return PrimaryServiceBinding.ToString("T"); } }
        public KuduBinding PrimaryServiceBinding { get; set; }
        public IEnumerable<KuduBinding> ServiceBindings { get; set; }
        public bool CustomHostNames { get; private set; }
        public RepositoryInfo RepositoryInfo { get; set; }
        public bool SupportsSni { get; set; }

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

        public IEnumerable<SelectListItem> Schemas { get; set; }
        public IEnumerable<SelectListItem> Certificates { get; set; }
        public IEnumerable<SelectListItem> IpAddresses { get; set; }
    }
}