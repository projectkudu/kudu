using System;
using System.Collections.Generic;
using System.Linq;
using Kudu.SiteManagement;

namespace Kudu.Web.Models
{
    public class ApplicationService : IApplicationService
    {
        private readonly ISiteManager _siteManager;

        public ApplicationService(ISiteManager siteManager)
        {
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
                ServiceUrl = site.ServiceUrl
            };
        }
    }

    public class SiteExistsException : InvalidOperationException
    {
    }

    public class SiteNotFoundException : InvalidOperationException
    {
    }
}