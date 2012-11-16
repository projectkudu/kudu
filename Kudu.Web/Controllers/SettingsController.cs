using System;
using System.Threading.Tasks;
using System.Web.Mvc;
using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.Settings;
using Kudu.Web.Models;
using Mvc.Async;

namespace Kudu.Web.Controllers
{
    public class SettingsController : TaskAsyncController
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

        public Task<ActionResult> Index(string slug)
        {
            return GetSettingsViewModel(slug).Then(model =>
            {
                if (model != null)
                {
                    return (ActionResult)View(model);
                }

                return HttpNotFound();
            });
        }

        [HttpPost]
        [ActionName("new-app-setting")]
        public Task<ActionResult> CreateAppSetting(string slug, string key, string value)
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

                    return RedirectToActionAsync("Index", new { slug });
                }
            }
            catch
            {
            }

            return GetSettingsViewModel(slug).Then(model =>
            {
                ViewBag.Key = key;
                ViewBag.Value = value;

                return (ActionResult)View("index", model);
            });
        }

        [HttpPost]
        [ActionName("new-connection-string")]
        public Task<ActionResult> CreateConnectionString(string slug, string name, string connectionString)
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

                    return RedirectToActionAsync("Index", new { slug });
                }
            }
            catch
            {
            }

            return GetSettingsViewModel(slug).Then(model =>
            {
                ViewBag.appName = slug;
                ViewBag.Name = name;
                ViewBag.ConnectionString = connectionString;

                return (ActionResult)View("index", model);
            });
        }

        [HttpPost]
        [ActionName("new-branch")]
        public Task<ActionResult> SetBranch(string slug, string branch)
        {
            if (String.IsNullOrEmpty(branch))
            {
                ModelState.AddModelError("Branch", "branch is required");
            }

            if (ModelState.IsValid)
            {
                var tcs = new TaskCompletionSource<ActionResult>();
                _service.SetKuduSetting(slug, "branch", branch)
                                .ContinueWith(task =>
                                {
                                    if (task.IsFaulted)
                                    {
                                        tcs.SetException(task.Exception.InnerExceptions);
                                    }
                                    else
                                    {
                                        tcs.SetResult(RedirectToAction("Index", new { slug }));
                                    }
                                });

                return tcs.Task;
            }

            return GetSettingsViewModel(slug).Then(model =>
            {
                ViewBag.appName = slug;
                ViewBag.branch = branch;

                return (ActionResult)View("index", model);
            });
        }

        [HttpPost]
        [ActionName("set-buildargs")]
        public Task<ActionResult> SetBuildArgs(string slug, string buildargs)
        {
            if (buildargs != null)
            {
                var tcs = new TaskCompletionSource<ActionResult>();
                _service.SetKuduSetting(slug, SettingsKeys.BuildArgs, buildargs)
                               .ContinueWith(task =>
                               {
                                   if (task.IsFaulted)
                                   {
                                       tcs.SetException(task.Exception.InnerExceptions);
                                   }
                                   else
                                   {
                                       tcs.SetResult(RedirectToAction("Index", new { slug }));
                                   }
                               });

                return tcs.Task;                
            }

            return GetSettingsViewModel(slug).Then(model =>
            {
                ViewBag.appName = slug;
                ViewBag.buildargs = buildargs;

                return (ActionResult)View("index", model);
            });
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

        private Task<SettingsViewModel> GetSettingsViewModel(string name)
        {
            ViewBag.slug = name;
            ViewBag.appName = name;

            try
            {
                return _service.GetSettings(name).ContinueWith(task =>
                {
                    if (task.IsFaulted)
                    {
                        // Read it
                        var ex = task.Exception;

                        return new SettingsViewModel
                        {
                            Enabled = false
                        };
                    }

                    return new SettingsViewModel
                    {
                        AppSettings = task.Result.AppSettings,
                        ConnectionStrings = task.Result.ConnectionStrings,
                        KuduSettings = task.Result.KuduSettings,
                        Enabled = true
                    };
                });
            }
            catch
            {
                var tcs = new TaskCompletionSource<SettingsViewModel>();
                tcs.SetResult(new SettingsViewModel
                {
                    Enabled = false
                });

                return tcs.Task;
            }
        }
    }
}
