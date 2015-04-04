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
            Site site = _siteManager.GetSite(name);
            if (site == null)
            {
                throw new SiteNotFoundException();
            }

            return new Application
            {
                Name = name,
                SiteBindings = site.SiteBindings,
                ServiceBindings = site.ServiceBindings
            };
        }

        public bool RemoveLiveSiteBinding(string name, KuduBinding siteBinding)
        {
            var application = GetApplication(name);
            if (application == null)
            {
                return false;
            }

            return _siteManager.RemoveSiteBinding(name, siteBinding, SiteType.Live);
        }

        public bool RemoveServiceSiteBinding(string name, KuduBinding siteBinding)
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