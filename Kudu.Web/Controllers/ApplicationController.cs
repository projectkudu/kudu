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
                return View(new ApplicationViewModel(application));
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
                        RepositoryType = (int)appViewModel.RepositoryType,
                        Created = DateTime.Now
                    };

                    // Give iis a chance to start the app up
                    // if we send requests too quickly, we'll end up getting 404s
                    Thread.Sleep(500);

                    IRepositoryManager repositoryManager = GetRepositoryManager(app);
                    repositoryManager.CreateRepository(appViewModel.RepositoryType);

                    db.Applications.Add(app);
                    db.SaveChanges();

                    return RedirectToAction("Index");
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
                var repositoryManager = GetRepositoryManager(application);
                var type = repositoryManager.GetRepositoryType();

                ViewBag.CloneUrl = GetCloneUrl(application, type);
                ViewBag.RepositoryType = type;
                ViewBag.AppName = application.Name;

                return View();
            }

            return HttpNotFound();
        }

        [ActionName("editor")]
        public ActionResult EditFiles(string slug) {
            Application application = db.Applications.SingleOrDefault(a => a.Slug == slug);
            if (application != null) {
                ViewBag.AppName = application.Name;
                return View();
            }

            return HttpNotFound();
        }

        //
        // GET: /Application/Delete/5

        public ActionResult Delete(string slug) {
            Application application = db.Applications.SingleOrDefault(a => a.Slug == slug);
            if (application != null) {
                return View(new ApplicationViewModel(application));
            }
            return HttpNotFound();
        }

        //
        // POST: /Application/Delete/5

        [HttpPost, ActionName("Delete")]
        public ActionResult DeleteConfirmed(string slug) {
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

        private string GetCloneUrl(Application application, RepositoryType type) {
            string prefix = application.ServiceUrl + application.Slug;
            return prefix + (type == RepositoryType.Git ? ".git" : String.Empty);
        }

        private void PopulateRepositoyTypes() {
            ViewBag.RepositoryType = Enum.GetNames(typeof(RepositoryType))
                                         .Select((name, value) => new SelectListItem {
                                             Text = name,
                                             Value = value.ToString()
                                         })
                                         .Skip(1);
        }

        private static IRepositoryManager GetRepositoryManager(Application application) {
            return new RemoteRepositoryManager(application.ServiceUrl + "scm");
        }
    }
}