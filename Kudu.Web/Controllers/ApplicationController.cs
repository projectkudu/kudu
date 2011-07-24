using System;
using System.Linq;
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
            return View(db.Applications.ToList().Select(a => new ApplicationViewModel(a)));
        }

        //
        // GET: /Application/Details/5

        public ActionResult Details(int id) {
            Application application = db.Applications.Find(id);
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
            if (db.Applications.Any(a => a.Name == appViewModel.Name)) {
                ModelState.AddModelError("Name", "Site already exists");
            }

            if (ModelState.IsValid) {

                Site site = null;

                try {
                    site = _siteManager.CreateSite(appViewModel.Name);

                    var app = new Application {
                        Name = appViewModel.Name,
                        ServiceUrl = site.ServiceUrl,
                        SiteUrl = site.SiteUrl,
                        SiteName = site.SiteName,
                        ServiceName = site.ServiceName,
                        RepositoryType = (int)appViewModel.RepositoryType
                    };

                    string repositoryUrl = app.ServiceUrl + "scm";
                    new RemoteRepositoryManager(repositoryUrl).CreateRepository(appViewModel.RepositoryType);

                    db.Applications.Add(app);
                    db.SaveChanges();

                    return RedirectToAction("Index");
                }
                catch (Exception ex) {
                    if (site != null) {
                        _siteManager.DeleteSite(site.ServiceName);
                        _siteManager.DeleteSite(site.SiteName);
                    }

                    ModelState.AddModelError("__FORM", ex.Message);
                }
            }

            PopulateRepositoyTypes();
            return View(appViewModel);
        }

        [ActionName("scm")]
        public ActionResult ViewSourceControl(int id) {
            return View(id);
        }

        [ActionName("editor")]
        public ActionResult EditFiles(int id) {
            return View(id);
        }

        //
        // GET: /Application/Delete/5

        public ActionResult Delete(int id) {
            Application application = db.Applications.Find(id);
            if (application != null) {
                return View(new ApplicationViewModel(application));
            }
            return HttpNotFound();
        }

        //
        // POST: /Application/Delete/5

        [HttpPost, ActionName("Delete")]
        public ActionResult DeleteConfirmed(int id) {
            Application application = db.Applications.Find(id);
            if (application != null) {

                _siteManager.DeleteSite(application.ServiceName);
                _siteManager.DeleteSite(application.SiteName);

                try {
                    string repositoryUrl = application.ServiceUrl + "scm";
                    new RemoteRepositoryManager(repositoryUrl).Delete();
                }
                catch {

                }

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
                                         })
                                         .Skip(1);
        }

    }
}