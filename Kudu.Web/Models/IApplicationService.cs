using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kudu.SiteManagement;
using Kudu.Core.SourceControl;
using Kudu.Client.SourceControl;
using Kudu.Client.Infrastructure;
using Kudu.Web.Infrastructure;

namespace Kudu.Web.Models
{
    public interface IApplicationService
    {
        IApplication AddApplication(string name);
        bool DeleteApplication(string name);
        void SetDeveloperSiteWebRoot(string name, string path);
        bool TryCreateDeveloperSite(string name, out string developerSiteUrl);
        IEnumerable<IApplication> GetApplications();
        IApplication GetApplication(string name);
    }

    public class SiteService : IApplicationService
    {
        private KuduContext _db;
        private ISiteManager _siteManager;
        private readonly ICredentialProvider _credentialProvider;

        public SiteService(KuduContext db, ISiteManager siteManager, ICredentialProvider credentialProvider)
        {
            _db = db;
            _siteManager = siteManager;
            _credentialProvider = credentialProvider;
        }

        public IApplication AddApplication(string name)
        {
            string slug = name.GenerateSlug();
            if (_db.Applications.Any(a => a.Name == name || a.Slug == slug))
            {
                throw new SiteExistsFoundException(slug);
            }

            Site site = null;
            IApplication app = null;

            try
            {
                site = _siteManager.CreateSite(slug);

                var newApp = new Application
                {
                    Name = name,
                    Slug = slug,
                    ServiceUrl = site.ServiceUrl,
                    SiteUrl = site.SiteUrl,
                    SiteName = slug,
                    Created = DateTime.Now,
                    UniqueId = Guid.NewGuid()
                };

                _db.Applications.Add(newApp);
                _db.SaveChanges();

                app = newApp;
            }
            catch
            {
                if (site != null)
                {
                    _siteManager.DeleteSite(name);
                }

                throw;
            }

            return app;
        }

        public bool DeleteApplication(string name)
        {
            string slug = name.GenerateSlug();

            Application application = _db.Applications.SingleOrDefault(a => a.Slug == name);
            if (application == null)
            {
                return false;
            }

            _siteManager.DeleteSite(application.Name);
            return true;
        }

        public void SetDeveloperSiteWebRoot(string name, string path)
        {
            throw new NotImplementedException();
        }

        public bool TryCreateDeveloperSite(string name, out string developerSiteUrl)
        {
            //developerSiteUrl = null;
            //Application application = _db.Applications.SingleOrDefault(a => a.Slug == slug);
            //if (application == null)
            //{
            //    throw new SiteNotFoundException(slug);
            //}

            //IRepositoryManager repositoryManager = GetRepositoryManager(application);
            //RepositoryType repositoryType = repositoryManager.GetRepositoryType();
            //var state = (DeveloperSiteState)application.DeveloperSiteState;

            //// Do nothing if the site is still being created
            //if (state != DeveloperSiteState.None ||
            //    repositoryType == RepositoryType.None)
            //{
            //    return false;
            //}

            //try
            //{
            //    application.DeveloperSiteState = (int)DeveloperSiteState.Creating;
            //    _db.SaveChanges();

            //    if (_siteManager.TryCreateDeveloperSite(slug, out developerSiteUrl))
            //    {
            //        // Clone the repository to the developer site
            //        var devRepositoryManager = new RemoteRepositoryManager(application.ServiceUrl + "dev/scm");
            //        devRepositoryManager.Credentials = _credentialProvider.GetCredentials();
            //        devRepositoryManager.CloneRepository(repositoryType);

            //        application.DeveloperSiteUrl = developerSiteUrl;
            //        _db.SaveChanges();

            //        return true;
            //    }
            //}
            //catch
            //{
            //    application.DeveloperSiteUrl = null;
            //    application.DeveloperSiteState = (int)DeveloperSiteState.None;
            //    _db.SaveChanges();
            //    throw;
            //}

            return false;
        }

        private IRepositoryManager GetRepositoryManager(Application application)
        {
            var repositoryManager = new RemoteRepositoryManager(application.ServiceUrl + "live/scm");
            repositoryManager.Credentials = _credentialProvider.GetCredentials();
            return repositoryManager;
        }


        public IEnumerable<IApplication> GetApplications()
        {
            return _db.Applications.ToList();
        }


        public IApplication GetApplication(string name)
        {
            string slug = name.GenerateSlug();
            return _db.Applications.Any(a => a.Slug == slug);
        }
    }

    public class SiteExistsFoundException : InvalidOperationException
    {
        private string _slug;

        public SiteExistsFoundException(string slug)
        {
            slug = slug;
        }
    }

    public class SiteNotFoundException : InvalidOperationException
    {
        private readonly string _slug;

        public SiteNotFoundException(string slug)
        {
            _slug = slug;
        }

    }
}
