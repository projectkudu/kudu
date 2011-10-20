using System;
using System.Linq;
using System.Threading;
using System.Web.Mvc;
using Kudu.Core.SourceControl;
using Kudu.Web.Infrastructure;
using Kudu.Web.Models;
using System.IO;
using Kudu.Core.Infrastructure;

namespace Kudu.Web.Controllers {
    public class ApplicationController : Controller {
        private KuduContext db = new KuduContext();
        private readonly ISiteManager _siteManager;

        public ApplicationController(ISiteManager siteManager) {
            _siteManager = siteManager;
        }

        //
        // GET: /Application/

        public ViewResult Index() {
            var applications = db.Applications.OrderBy(a => a.Created);
            return View(applications.ToList().Select(a => new ApplicationViewModel(a)));
        }

        //
        // GET: /Application/Details/5

        public ActionResult Details(string slug) {
            Application application = db.Applications.SingleOrDefault(a => a.Slug == slug);
            if (application != null) {
                var appViewModel = new ApplicationViewModel(application);
                appViewModel.RepositoryType = GetRepositoryManager(application).GetRepositoryType();

                return View(appViewModel);
            }
            return HttpNotFound();
        }

        //
        // GET: /Application/Create

        public ActionResult Create() {
            PopulateRepositoyTypes();
            return View();
        }

        //
        // POST: /Application/Create

        [HttpPost]
        public ActionResult Create(ApplicationViewModel appViewModel) {
            string slug = appViewModel.Name.GenerateSlug();
            if (db.Applications.Any(a => a.Name == appViewModel.Name || a.Slug == slug)) {
                ModelState.AddModelError("Name", "Site already exists");
            }

            if (ModelState.IsValid) {
                Site site = null;

                try {
                    site = _siteManager.CreateSite(slug);

                    var app = new Application {
                        Name = appViewModel.Name,
                        Slug = slug,
                        ServiceUrl = site.ServiceUrl,
                        SiteUrl = site.SiteUrl,
                        ServiceAppName = site.ServiceAppName,
                        SiteName = site.SiteName,
                        Created = DateTime.Now,
                        UniqueId = Guid.NewGuid()
                    };

                    // Give iis a chance to start the app up
                    // if we send requests too quickly, we'll end up getting 404s
                    Thread.Sleep(500);

                    if (appViewModel.RepositoryType != RepositoryType.None) {
                        IRepositoryManager repositoryManager = GetRepositoryManager(app);
                        repositoryManager.CreateRepository(appViewModel.RepositoryType);
                    }

                    db.Applications.Add(app);
                    db.SaveChanges();

                    return RedirectToAction("Details", new { slug = slug });
                }
                catch (Exception ex) {
                    if (site != null) {
                        _siteManager.DeleteSite(slug);
                    }

                    ModelState.AddModelError("__FORM", ex.Message);
                }
            }

            PopulateRepositoyTypes();
            return View(appViewModel);
        }

        [ActionName("scm")]
        public ActionResult ViewSourceControl(string slug) {
            Application application = db.Applications.SingleOrDefault(a => a.Slug == slug);
            if (application != null) {
                var appViewModel = new ApplicationViewModel(application);
                appViewModel.RepositoryType = GetRepositoryManager(application).GetRepositoryType();

                return View(appViewModel);
            }

            return HttpNotFound();
        }

        [ActionName("deployments")]
        public ActionResult ViewDeployments(string slug) {
            Application application = db.Applications.SingleOrDefault(a => a.Slug == slug);
            if (application != null) {
                var appViewModel = new ApplicationViewModel(application);
                appViewModel.RepositoryType = GetRepositoryManager(application).GetRepositoryType();

                return View(appViewModel);
            }

            return HttpNotFound();
        }

        [ActionName("editor")]
        public ActionResult EditFiles(string slug) {
            Application application = db.Applications.SingleOrDefault(a => a.Slug == slug);
            if (application == null) {
                return HttpNotFound();
            }

            var appViewModel = new ApplicationViewModel(application);
            var repositoryManager = GetRepositoryManager(application);
            RepositoryType repositoryType = repositoryManager.GetRepositoryType();

            if (application.DeveloperSiteUrl == null) {
                if (repositoryType != RepositoryType.None) {
                    // Set this flag so we know that we're in the state where we can
                    // create the developer site.
                    ViewBag.Clone = true;
                }

                appViewModel.RepositoryType = RepositoryType.None;
            }
            else {
                appViewModel.RepositoryType = repositoryType;
            }

            return View(appViewModel);
        }

        [HttpPost]
        [ActionName("default-project")]
        public ActionResult SetWebRoot(string slug, string projectPath) {
            Application application = db.Applications.SingleOrDefault(a => a.Slug == slug);
            if (application == null) {
                return HttpNotFound();
            }

            _siteManager.SetDeveloperSiteWebRoot(application.Name, projectPath);

            return new EmptyResult();
        }

        [HttpPost]
        [ActionName("clone")]
        public ActionResult CreateDeveloperSite(string slug) {
            Application application = db.Applications.SingleOrDefault(a => a.Slug == slug);
            if (application == null) {
                return HttpNotFound();
            }

            IRepositoryManager repositoryManager = GetRepositoryManager(application);
            RepositoryType repositoryType = repositoryManager.GetRepositoryType();
            var state = (DeveloperSiteState)application.DeveloperSiteState;

            if (application.DeveloperSiteUrl != null || 
                state == DeveloperSiteState.Creating || 
                repositoryType == RepositoryType.None) {
                return new EmptyResult();
            }

            try {
                application.DeveloperSiteState = (int)DeveloperSiteState.Creating;
                db.SaveChanges();

                // Get the deployment repository path
                string repositoryPath = PathHelper.GetDeploymentRepositoryPath(application.Name);

                FileSystemHelpers.EnsureDirectory(PathHelper.GetDeveloperApplicationPath(application.Name));

                // Clone the repository to the developer site
                var devRepositoryManager = new RemoteRepositoryManager(application.ServiceUrl + "dev/scm");
                devRepositoryManager.CloneRepository(repositoryPath, repositoryType);

                string developerSiteUrl;
                if (_siteManager.TryCreateDeveloperSite(slug, out developerSiteUrl)) {
                    application.DeveloperSiteUrl = developerSiteUrl;
                    db.SaveChanges();
                }
            }
            catch {
                application.DeveloperSiteUrl = null;
                application.DeveloperSiteState = (int)DeveloperSiteState.None;
                db.SaveChanges();
                throw;
            }

            return new EmptyResult();
        }

        //
        // POST: /Application/Delete/5

        [HttpPost]
        public ActionResult Delete(string slug) {
            Application application = db.Applications.SingleOrDefault(a => a.Slug == slug);
            if (application != null) {
                try {
                    IRepositoryManager repositoryManager = GetRepositoryManager(application);
                    repositoryManager.Delete();
                }
                catch {
                }

                _siteManager.DeleteSite(slug);

                db.Applications.Remove(application);
                db.SaveChanges();

                return RedirectToAction("Index");
            }

            return HttpNotFound();
        }

        protected override void Dispose(bool disposing) {
            db.Dispose();
            base.Dispose(disposing);
        }

        private void PopulateRepositoyTypes() {
            ViewBag.RepositoryType = Enum.GetNames(typeof(RepositoryType))
                                         .Select((name, value) => new SelectListItem {
                                             Text = name,
                                             Value = value.ToString()
                                         });
        }

        private static IRepositoryManager GetRepositoryManager(Application application) {
            return new RemoteRepositoryManager(application.ServiceUrl + "live/scm");
        }
    }
}