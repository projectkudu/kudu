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
            if (GetApplications().Any(x => x == name))
            {
                throw new SiteExistsException();
            }

            _siteManager.CreateSite(name);
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
            return _siteManager.GetSites();
        }

        public IApplication GetApplication(string name)
        {
            var site = _siteManager.GetSite(name);

            return new Application
            {
                Name = name,
                SiteUrl = site.SiteUrl,
                DevSiteUrl = site.DevSiteUrl,
                ServiceUrl = site.ServiceUrl
            };
        }

        public void CreateDevelopmentSite(string name)
        {
            string siteUrl;
            _siteManager.TryCreateDeveloperSite(name, out siteUrl);
        }
    }

    public class SiteExistsException : InvalidOperationException
    {
    }

    public class SiteNotFoundException : InvalidOperationException
    {
    }
}