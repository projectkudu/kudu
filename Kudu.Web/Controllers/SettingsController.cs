using System;
using System.Linq;
using System.Web.Mvc;
using Kudu.Core.Deployment;
using Kudu.Web.Models;

namespace Kudu.Web.Controllers {
    public class SettingsController : Controller {
        private readonly KuduContext db = new KuduContext();

        public ActionResult Index(string slug) {
            SettingsViewModel model = GetSettingsViewModel(slug);

            if (model != null) {
                return View(model);
            }

            return HttpNotFound();
        }

        [HttpPost]
        [ActionName("new-app-setting")]
        public ActionResult CreateAppSetting(string slug, string key, string value) {
            try {
                if (String.IsNullOrEmpty(key)) {
                    ModelState.AddModelError("Key", "key is required");
                }
                if (String.IsNullOrEmpty(value)) {
                    ModelState.AddModelError("Value", "value is required");
                }

                if (ModelState.IsValid) {
                    Application application = db.Applications.SingleOrDefault(a => a.Slug == slug);
                    var settingsManager = new RemoteDeploymentSettingsManager(application.ServiceUrl);

                    settingsManager.SetAppSetting(key, value);

                    return RedirectToAction("Index", new { slug });
                }
            }
            catch {
            }

            SettingsViewModel model = GetSettingsViewModel(slug);
            ViewBag.Key = key;
            ViewBag.Value = value;

            return View("index", model);
        }

        [HttpPost]
        [ActionName("new-connection-string")]
        public ActionResult CreateConnectionString(string slug, string name, string connectionString) {
            try {
                if (String.IsNullOrEmpty(name)) {
                    ModelState.AddModelError("Name", "name is required");
                }
                if (String.IsNullOrEmpty(connectionString)) {
                    ModelState.AddModelError("ConnectionString", "connection string is required");
                }

                if (ModelState.IsValid) {
                    Application application = db.Applications.SingleOrDefault(a => a.Slug == slug);
                    var settingsManager = new RemoteDeploymentSettingsManager(application.ServiceUrl);

                    settingsManager.SetConnectionString(name, connectionString);

                    return RedirectToAction("Index", new { slug });
                }
            }
            catch {
            }

            SettingsViewModel model = GetSettingsViewModel(slug);
            ViewBag.Name = name;
            ViewBag.ConnectionString = connectionString;

            return View("index", model);
        }
        
        [HttpPost]
        [ActionName("delete-connection-string")]
        public ActionResult DeleteConnectionString(string slug, string name) {
            Application application = db.Applications.SingleOrDefault(a => a.Slug == slug);
            var settingsManager = new RemoteDeploymentSettingsManager(application.ServiceUrl);

            settingsManager.RemoveConnectionString(name);

            return RedirectToAction("Index", new { slug });
        }

        [HttpPost]
        [ActionName("delete-app-setting")]
        public ActionResult DeleteApplicationSetting(string slug, string key) {
            Application application = db.Applications.SingleOrDefault(a => a.Slug == slug);
            var settingsManager = new RemoteDeploymentSettingsManager(application.ServiceUrl);

            settingsManager.RemoveAppSetting(key);

            return RedirectToAction("Index", new { slug });
        }

        private SettingsViewModel GetSettingsViewModel(string slug) {
            Application application = db.Applications.SingleOrDefault(a => a.Slug == slug);
            if (application != null) {
                return GetSettingsViewModel(application);
            }

            return null;
        }

        private SettingsViewModel GetSettingsViewModel(Application application) {
            var settingsManager = new RemoteDeploymentSettingsManager(application.ServiceUrl);

            ViewBag.slug = application.Slug;

            return new SettingsViewModel {
                AppSettings = settingsManager.GetAppSettings().ToList(),
                ConnectionStrings = settingsManager.GetConnectionStrings().ToList()
            };
        }
    }
}
