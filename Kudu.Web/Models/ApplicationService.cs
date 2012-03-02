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

        public void AddApplication(string userName, string name)
        {
            if (_db.Applications.Any(a => a.Name == name))
            {
                throw new SiteExistsFoundException();
            }

            Site site = null;

            try
            {
                site = _siteManager.CreateSite(name);

                var newApp = new KuduApplication
                {
                    Name = name,
                    Username = userName,
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

        public bool DeleteApplication(string userName, string name)
        {
            KuduApplication application = _db.Applications.SingleOrDefault(a => a.Name == name);
            if (application == null)
            {
                return false;
            }

            _siteManager.DeleteSite(application.Name);

            _db.Applications.Remove(application);
            _db.SaveChanges();

            return true;
        }

        public IEnumerable<string> GetApplications(string userName)
        {
            return (from a in _db.Applications
                    where a.Username == userName
                    select a.Name).ToList();
        }


        public IApplication GetApplication(string userName, string name)
        {
            return _db.Applications.FirstOrDefault(a => a.Name == name && a.Username == userName);
        }
    }

    public class SiteExistsFoundException : InvalidOperationException
    {
    }

    public class SiteNotFoundException : InvalidOperationException
    {
    }
}