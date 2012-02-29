using System;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using Kudu.Client.Infrastructure;
using Kudu.Web.Infrastructure;
using Kudu.Web.Models;

namespace Kudu.Web.Controllers
{
    public class SettingsController : Controller
    {
        private readonly IApplicationService _applicationService;
        private readonly ICredentialProvider _credentialProvider;

        public SettingsController(IApplicationService applicationService,
                                  ICredentialProvider credentialProvider)
        {
            _applicationService = applicationService;
            _credentialProvider = credentialProvider;
        }

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            ViewBag.tab = "configuration";

            base.OnActionExecuting(filterContext);
        }

        public ActionResult Index(string slug)
        {
            SettingsViewModel model = GetSettingsViewModel(slug);

            if (model != null)
            {
                return View(model);
            }

            return HttpNotFound();
        }

        [HttpPost]
        [ActionName("new-app-setting")]
        public ActionResult CreateAppSetting(string slug, string key, string value)
        {
            IApplication application = _applicationService.GetApplication(slug);
            
            if (application == null)
            {
                return HttpNotFound();
            }

            try
            {
                if (String.IsNullOrEmpty(key))
                {
                    ModelState.AddModelError("Key", "key is required");
                }
                if (String.IsNullOrEmpty(value))
                {
                    ModelState.AddModelError("Value", "value is required");
                }

                if (ModelState.IsValid)
                {
                    ICredentials credentials = _credentialProvider.GetCredentials();
                    var settingsManager = application.GetSettingsManager(credentials);

                    settingsManager.SetAppSetting(key, value);

                    return RedirectToAction("Index", new { slug });
                }
            }
            catch
            {
            }

            SettingsViewModel model = GetSettingsViewModel(slug);
            ViewBag.Key = key;
            ViewBag.Value = value;

            return View("index", model);
        }

        [HttpPost]
        [ActionName("new-connection-string")]
        public ActionResult CreateConnectionString(string slug, string name, string connectionString)
        {
            IApplication application = _applicationService.GetApplication(slug);

            if (application == null)
            {
                return HttpNotFound();
            }

            try
            {
                if (String.IsNullOrEmpty(name))
                {
                    ModelState.AddModelError("Name", "name is required");
                }
                if (String.IsNullOrEmpty(connectionString))
                {
                    ModelState.AddModelError("ConnectionString", "connection string is required");
                }

                if (ModelState.IsValid)
                {
                    ICredentials credentials = _credentialProvider.GetCredentials();
                    var settingsManager = application.GetSettingsManager(credentials);

                    settingsManager.SetConnectionString(name, connectionString);

                    return RedirectToAction("Index", new { slug });
                }
            }
            catch
            {
            }

            SettingsViewModel model = GetSettingsViewModel(slug);
            ViewBag.appName = model.Application.Name;
            ViewBag.Name = name;
            ViewBag.ConnectionString = connectionString;

            return View("index", model);
        }

        [HttpPost]
        [ActionName("delete-connection-string")]
        public ActionResult DeleteConnectionString(string slug, string name)
        {
            IApplication application = _applicationService.GetApplication(slug);
            if (application == null)
            {
                return HttpNotFound();
            }

            ICredentials credentials = _credentialProvider.GetCredentials();
            var settingsManager = application.GetSettingsManager(credentials);

            settingsManager.RemoveConnectionString(name);

            return RedirectToAction("Index", new { slug });
        }

        [HttpPost]
        [ActionName("delete-app-setting")]
        public ActionResult DeleteApplicationSetting(string slug, string key)
        {
            IApplication application = _applicationService.GetApplication(slug);
            if (application == null)
            {
                return HttpNotFound();
            }

            ICredentials credentials = _credentialProvider.GetCredentials();
            var settingsManager = application.GetSettingsManager(credentials);
            settingsManager.RemoveAppSetting(key);

            return RedirectToAction("Index", new { slug });
        }

        private SettingsViewModel GetSettingsViewModel(string slug)
        {
            IApplication application = _applicationService.GetApplication(slug);

            if (application != null)
            {
                return GetSettingsViewModel(application);
            }

            return null;
        }

        private SettingsViewModel GetSettingsViewModel(IApplication application)
        {
            ICredentials credentials = _credentialProvider.GetCredentials();
            var settingsManager = application.GetSettingsManager(credentials);

            ViewBag.slug = application.Name;
            ViewBag.appName = application.Name;

            try
            {
                return new SettingsViewModel
                {
                    AppSettings = settingsManager.GetAppSettings().ToList(),
                    ConnectionStrings = settingsManager.GetConnectionStrings().ToList(),
                    Application = new ApplicationViewModel(application),
                    Enabled = true
                };
            }
            catch
            {
                return new SettingsViewModel
                {
                    Application = new ApplicationViewModel(application),
                    Enabled = false
                };
            }
        }
    }
}
