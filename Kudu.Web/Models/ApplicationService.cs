using System;
using System.Collections.Generic;
using System.Linq;
using Kudu.SiteManagement;

namespace Kudu.Web.Models
{
    public class ApplicationService : IApplicationService
    {
        private readonly KuduContext _db;
        private readonly ISiteManager _siteManager;

        public ApplicationService(KuduContext db, ISiteManager siteManager)
        {
            _db = db;
            _siteManager = siteManager;
        }

        public void AddApplication(string name)
        {
            AddApplication(name, null, 0);
        }

        public void AddApplication(string name, string hostname, int port)
        {
            if (GetApplications().Any(x => x == name))
            {
                throw new SiteExistsFoundException();
            }

            _siteManager.CreateSite(name, hostname, port);
        }

        public bool DeleteApplication(string name)
        {
            var application = GetApplication(name);
            if (application == null)
            {
                return false;
            }

            _siteManager.DeleteSite(name);
            return true;
        }

        public IEnumerable<string> GetApplications()
        {
            var sites = _siteManager.GetSites();
            const string sitePrefix = "kudu_";

            // Selects only the live sites
            return sites
                .Where(x => x.StartsWith(sitePrefix) && !x.StartsWith(sitePrefix + "dev_") && !x.StartsWith(sitePrefix + "service_"))
                .Select(x => x.Split('_')[1]);
        }

        public IApplication GetApplication(string name)
        {
            var site = _siteManager.GetSite(name);
            return new Application { Name = name, SiteUrl = site.SiteUrl, DevSiteUrl = site.DevSiteUrl, ServiceUrl = site.ServiceUrl };
        }

        public void CreateDevelopmentSite(string name)
        {
            string siteUrl;
            _siteManager.TryCreateDeveloperSite(name, out siteUrl);
        }
    }

    public class SiteExistsFoundException : InvalidOperationException
    {
    }

    public class SiteNotFoundException : InvalidOperationException
    {
    }
}