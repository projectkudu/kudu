using System;
using System.Web.Mvc;
using Kudu.Web.Models;

namespace Kudu.Web.Controllers
{
    public class SettingsController : Controller
    {
        private readonly ISettingsService _service;

        public SettingsController(ISettingsService service)
        {
            _service = service;
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
                    _service.SetAppSetting(slug, key, value);

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
                    _service.SetConnectionString(slug, name, connectionString);

                    return RedirectToAction("Index", new { slug });
                }
            }
            catch
            {
            }

            SettingsViewModel model = GetSettingsViewModel(slug);
            ViewBag.appName = slug;
            ViewBag.Name = name;
            ViewBag.ConnectionString = connectionString;

            return View("index", model);
        }

        [HttpPost]
        [ActionName("delete-connection-string")]
        public ActionResult DeleteConnectionString(string slug, string name)
        {
            _service.RemoveConnectionString(slug, name);

            return RedirectToAction("Index", new { slug });
        }

        [HttpPost]
        [ActionName("delete-app-setting")]
        public ActionResult DeleteApplicationSetting(string slug, string key)
        {
            _service.RemoveAppSetting(slug, key);

            return RedirectToAction("Index", new { slug });
        }

        private SettingsViewModel GetSettingsViewModel(string name)
        {
            ViewBag.slug = name;
            ViewBag.appName = name;

            try
            {
                ISettings settings = _service.GetSettings(name);

                return new SettingsViewModel
                {
                    AppSettings = settings.AppSettings,
                    ConnectionStrings = settings.ConnectionStrings,
                    Enabled = true
                };
            }
            catch
            {
                return new SettingsViewModel
                {
                    Enabled = false
                };
            }
        }
    }
}
