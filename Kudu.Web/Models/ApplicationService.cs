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
            if (_db.Applications.Any(a => a.Name == name))
            {
                throw new SiteExistsFoundException();
            }

            Site site = null;
            
            try
            {
                site = _siteManager.CreateSite(name);

                var newApp = new Application
                {
                    Name = name,
                    ServiceUrl = site.ServiceUrl,
                    SiteUrl = site.SiteUrl
                };

                _db.Applications.Add(newApp);
                _db.SaveChanges();
            }
            catch
            {
                if (site != null)
                {
                    _siteManager.DeleteSite(name);
                }

                throw;
            }
        }

        public bool DeleteApplication(string name)
        {            
            Application application = _db.Applications.SingleOrDefault(a => a.Name == name);
            if (application == null)
            {
                return false;
            }

            _siteManager.DeleteSite(application.Name);

            _db.Applications.Remove(application);
            _db.SaveChanges();

            return true;
        }

        public IEnumerable<string> GetApplications()
        {
            return (from a in _db.Applications
                    select a.Name).ToList();
        }


        public IApplication GetApplication(string name)
        {
            return _db.Applications.FirstOrDefault(a => a.Name == name);
        }

        public string CreateDevelopmentSite(string name)
        {
            string siteUrl;
            _siteManager.TryCreateDeveloperSite(name, out siteUrl);
            return siteUrl;
        }
    }

    public class SiteExistsFoundException : InvalidOperationException
    {
    }

    public class SiteNotFoundException : InvalidOperationException
    {
    }
}