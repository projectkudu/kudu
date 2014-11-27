using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

        public Task AddApplication(string name)
        {
            if (GetApplications().Any(x => x == name))
            {
                throw new SiteExistsException();
            }

            return _siteManager.CreateSiteAsync(name);
        }

        public async Task<bool> DeleteApplication(string name)
        {
            var application = GetApplication(name);
            if (application == null)
            {
                return false;
            }

            await _siteManager.DeleteSiteAsync(name);
            return true;
        }

        public IEnumerable<string> GetApplications()
        {
            return _siteManager.GetSites();
        }

        public IApplication GetApplication(string name)
        {
            var site = _siteManager.GetSite(name);
            if (site == null)
            {
                throw new SiteNotFoundException();
            }

            return new Application
            {
                Name = name,
                SiteUrls = site.SiteUrls,
                ServiceUrls = site.ServiceUrls
            };
        }

        public bool RemoveLiveSiteBinding(string name, string siteBinding)
        {
            var application = GetApplication(name);
            if (application == null)
            {
                return false;
            }

            return _siteManager.RemoveSiteBinding(name, siteBinding, SiteType.Live);
        }

        public bool RemoveServiceSiteBinding(string name, string siteBinding)
        {
            var application = GetApplication(name);
            if (application == null)
            {
                return false;
            }

            return _siteManager.RemoveSiteBinding(name, siteBinding, SiteType.Service);
        }

        public bool AddSiteBinding(string name, KuduBinding binding)
        {
            var application = GetApplication(name);
            if (application == null)
            {
                return false;
            }

            return _siteManager.AddSiteBinding(name, binding);
        }
    }

    public class SiteExistsException : InvalidOperationException
    {
    }

    public class SiteNotFoundException : InvalidOperationException
    {
    }



    
}