using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using Kudu.Contracts.Settings;
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

        public async Task<ActionResult> Index(string slug)
        {
            var model = await GetSettingsViewModel(slug);

            if (model != null)
            {
                return (ActionResult)View(model);
            }

            return HttpNotFound();
        }

        [HttpPost]
        [ActionName("new-app-setting")]
        public async Task<ActionResult> CreateAppSetting(string slug, string key, string value)
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

            var model = await GetSettingsViewModel(slug);

            ViewBag.Key = key;
            ViewBag.Value = value;

            return (ActionResult)View("index", model);
        }

        [HttpPost]
        [ActionName("new-connection-string")]
        public async Task<ActionResult> CreateConnectionString(string slug, string name, string connectionString)
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

            var model = await GetSettingsViewModel(slug);

            ViewBag.appName = slug;
            ViewBag.Name = name;
            ViewBag.ConnectionString = connectionString;

            return (ActionResult)View("index", model);
        }

        [HttpPost]
        [ActionName("new-branch")]
        public async Task<ActionResult> SetBranch(string slug, string branch)
        {
            if (String.IsNullOrEmpty(branch))
            {
                ModelState.AddModelError("Branch", "branch is required");
            }

            if (ModelState.IsValid)
            {
                await _service.SetKuduSetting(slug, SettingsKeys.DeploymentBranch, branch);

                return RedirectToAction("Index", new { slug });
            }

            var model = await GetSettingsViewModel(slug);

            ViewBag.appName = slug;
            ViewBag.branch = branch;

            return (ActionResult)View("index", model);
        }

        [HttpPost]
        [ActionName("set-buildargs")]
        public async Task<ActionResult> SetBuildArgs(string slug, string buildargs)
        {
            if (buildargs != null)
            {
                await _service.SetKuduSetting(slug, SettingsKeys.BuildArgs, buildargs);

                return RedirectToAction("Index", new { slug });
            }

            var model = await GetSettingsViewModel(slug);

            ViewBag.appName = slug;
            ViewBag.buildargs = buildargs;

            return (ActionResult)View("index", model);
        }

        [HttpPost]
        [ActionName("set-customproperties")]
        public async Task<ActionResult> SetCustomProperties(string slug, IDictionary<string, string> settings)
        {
            if (settings != null && settings.Count > 0)
            {
                // validate custom property name/values
                int i = 0;
                foreach (var setting in settings)
                {
                    if (string.IsNullOrWhiteSpace(setting.Key))
                    {
                        ModelState.AddModelError(String.Format("Settings[{0}].Key", i), "property name cannot be empty");
                    }

                    if (string.IsNullOrWhiteSpace(setting.Value))
                    {
                        ModelState.AddModelError(String.Format("Settings[{0}].Value", i), "property value cannot be empty");
                    }
                    i++;
                }

                if (ModelState.IsValid)
                {
                    await _service.SetKuduSettings(slug, settings.ToArray());
                    return RedirectToAction("Index", new { slug });
                }
            }

            var model = await GetSettingsViewModel(slug);

            model.KuduSettings.SiteSettings = settings;

            return (ActionResult)View("index", model);
        }

        [HttpPost]
        [ActionName("add-customproperty")]
        public async Task<ActionResult> AddCustomProperty(string slug, string key, string value)
        {
            if (String.IsNullOrWhiteSpace(key))
            {
                ModelState.AddModelError("Key", "property name is required");
            }

            if (String.IsNullOrWhiteSpace(value))
            {
                ModelState.AddModelError("Value", "property value is required");
            }

            if (DeploymentSettingsViewModel.ReservedSettingKeys.Contains(key))
            {
                ModelState.AddModelError("Key", "this is a reserved property name");
            }

            if (ModelState.IsValid)
            {
                await _service.SetKuduSetting(slug, key, value);

                return RedirectToAction("Index", new { slug });
            }

            var model = await GetSettingsViewModel(slug);

            ViewBag.Key = key;
            ViewBag.Value = value;

            return (ActionResult)View("index", model);
        }

        [HttpPost]
        [ActionName("remove-customproperty")]
        public async Task<ActionResult> RemoveCustomProperty(string slug, string key)
        {
            if (String.IsNullOrEmpty(key))
            {
                ModelState.AddModelError("Key", "key is required");
            }

            if (ModelState.IsValid)
            {
                await _service.RemoveKuduSetting(slug, key);

                return RedirectToAction("Index", new { slug });
            }

            var model = await GetSettingsViewModel(slug);

            ViewBag.Key = key;

            return (ActionResult)View("index", model);
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

        private async Task<SettingsViewModel> GetSettingsViewModel(string name)
        {
            ViewBag.slug = name;
            ViewBag.appName = name;

            try
            {
                ISettings settings = await _service.GetSettings(name);

                return new SettingsViewModel
                {
                    AppSettings = settings.AppSettings,
                    ConnectionStrings = settings.ConnectionStrings,
                    KuduSettings = new DeploymentSettingsViewModel(settings.KuduSettings),
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
