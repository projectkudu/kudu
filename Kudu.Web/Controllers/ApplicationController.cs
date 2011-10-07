using System;
using System.Linq;
using System.Threading;
using System.Web.Mvc;
using Kudu.Core.SourceControl;
using Kudu.Web.Infrastructure;
using Kudu.Web.Models;

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
                        _siteManager.DeleteSite(site.SiteName, site.ServiceAppName);
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
            if (application != null) {
                ViewBag.AppName = application.Name;
                return View(new ApplicationViewModel(application));
            }

            return HttpNotFound();
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

                _siteManager.DeleteSite(application.SiteName, application.ServiceAppName);

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
            return new RemoteRepositoryManager(application.ServiceUrl + "scm");
        }
    }
}