using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using Kudu.Client.Deployment;
using Kudu.Client.Infrastructure;
using Kudu.Client.SourceControl;
using Kudu.Core.SourceControl;
using Kudu.SiteManagement;
using Kudu.Web.Infrastructure;
using Kudu.Web.Models;
using Mvc.Async;

namespace Kudu.Web.Controllers
{
    public class ApplicationController : TaskAsyncController
    {
        private KuduContext db = new KuduContext();
        private readonly ISiteManager _siteManager;
        private readonly ICredentialProvider _credentialProvider;
        private readonly KuduEnvironment _env;

        public ApplicationController(ISiteManager siteManager, ICredentialProvider credentialProvider, KuduEnvironment env)
        {
            _siteManager = siteManager;
            _credentialProvider = credentialProvider;
            _env = env;
        }

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            ViewBag.showAdmingWarning = !_env.IsAdmin && _env.RunningAgainstLocalKuduService;
            base.OnActionExecuting(filterContext);
        }

        //
        // GET: /Application/

        public ViewResult Index()
        {
            var applications = db.Applications.OrderBy(a => a.Created);
            return View(applications.ToList().Select(a => new ApplicationViewModel(a)));
        }

        public ActionResult Settings(string slug)
        {
            Application application = db.Applications.SingleOrDefault(a => a.Slug == slug);
            if (application != null)
            {
                var appViewModel = new ApplicationViewModel(application);
                appViewModel.RepositoryType = GetRepositoryManager(application).GetRepositoryType();

                ViewBag.slug = slug;
                ViewBag.tab = "settings";
                ViewBag.appName = appViewModel.Name;

                return View(appViewModel);
            }
            return HttpNotFound();
        }

        //
        // GET: /Application/Create

        public ActionResult Create()
        {
            return View();
        }

        //
        // POST: /Application/Create

        [HttpPost]
        public ActionResult Create(ApplicationViewModel appViewModel)
        {
            string slug = appViewModel.Name.GenerateSlug();
            if (db.Applications.Any(a => a.Name == appViewModel.Name || a.Slug == slug))
            {
                ModelState.AddModelError("Name", "Site already exists");
            }

            if (ModelState.IsValid)
            {
                Site site = null;

                try
                {
                    site = _siteManager.CreateSite(slug);

                    var app = new Application
                    {
                        Name = appViewModel.Name,
                        Slug = slug,
                        ServiceUrl = site.ServiceUrl,
                        SiteUrl = site.SiteUrl,
                        SiteName = slug,
                        Created = DateTime.Now,
                        UniqueId = Guid.NewGuid()
                    };

                    if (appViewModel.RepositoryType != RepositoryType.None)
                    {
                        IRepositoryManager repositoryManager = GetRepositoryManager(app);
                        repositoryManager.CreateRepository(appViewModel.RepositoryType);
                    }

                    db.Applications.Add(app);
                    db.SaveChanges();

                    return RedirectToAction("Settings", new { slug = slug });
                }
                catch (Exception ex)
                {
                    if (site != null)
                    {
                        _siteManager.DeleteSite(slug);
                    }

                    ModelState.AddModelError("__FORM", ex.Message);
                }
            }

            return View(appViewModel);
        }

        public Task<ActionResult> Deploy(string slug, string id)
        {
            Application application = db.Applications.SingleOrDefault(a => a.Slug == slug);
            if (application != null)
            {
                var deploymentManager = new RemoteDeploymentManager(application.ServiceUrl + "/deployments");

                return deploymentManager.DeployAsync(id).ContinueWith(task =>
                {
                    return (ActionResult)RedirectToAction("Deployments", new { slug = slug });
                });
            }

            return Task.Factory.StartNew(() => (ActionResult)HttpNotFound());
        }

        [ActionName("deployments")]
        public Task<ActionResult> ViewDeployments(string slug)
        {
            Application application = db.Applications.SingleOrDefault(a => a.Slug == slug);
            if (application != null)
            {
                var deploymentManager = new RemoteDeploymentManager(application.ServiceUrl + "/deployments");

                return deploymentManager.GetResultsAsync().ContinueWith(task =>
                {
                    var appViewModel = new ApplicationViewModel(application);
                    appViewModel.RepositoryType = GetRepositoryManager(application).GetRepositoryType();
                    appViewModel.Deployments = task.Result.ToList();

                    ViewBag.slug = slug;
                    ViewBag.tab = "deployments";
                    ViewBag.appName = appViewModel.Name;

                    return (ActionResult)View(appViewModel);
                });
            }

            return Task.Factory.StartNew(() => (ActionResult)HttpNotFound());
        }

        [ActionName("editor")]
        public ActionResult Editor(string slug)
        {
            Application application = db.Applications.SingleOrDefault(a => a.Slug == slug);
            if (application == null)
            {
                return HttpNotFound();
            }

            var appViewModel = new ApplicationViewModel(application);
            var repositoryManager = GetRepositoryManager(application);
            var siteState = (DeveloperSiteState)application.DeveloperSiteState;
            RepositoryType repositoryType = repositoryManager.GetRepositoryType();

            if (application.DeveloperSiteUrl == null)
            {
                if (repositoryType != RepositoryType.None &&
                    siteState == DeveloperSiteState.None)
                {
                    // Set this flag so we know that we're in the state where we can
                    // create the developer site.
                    ViewBag.Clone = true;
                }

                appViewModel.RepositoryType = RepositoryType.None;
            }
            else
            {
                appViewModel.RepositoryType = repositoryType;
            }

            return View(appViewModel);
        }

        [HttpPost]
        [ActionName("set-webroot")]
        public ActionResult SetWebRoot(string slug, string projectPath)
        {
            Application application = db.Applications.SingleOrDefault(a => a.Slug == slug);
            if (application == null)
            {
                return HttpNotFound();
            }

            _siteManager.SetDeveloperSiteWebRoot(application.Name, Path.GetDirectoryName(projectPath));

            return new EmptyResult();
        }

        [HttpPost]
        [ActionName("create-dev-site")]
        public ActionResult CreateDeveloperSite(string slug)
        {
            Application application = db.Applications.SingleOrDefault(a => a.Slug == slug);
            if (application == null)
            {
                return HttpNotFound();
            }

            IRepositoryManager repositoryManager = GetRepositoryManager(application);
            RepositoryType repositoryType = repositoryManager.GetRepositoryType();
            var state = (DeveloperSiteState)application.DeveloperSiteState;

            // Do nothing if the site is still being created
            if (state != DeveloperSiteState.None ||
                repositoryType == RepositoryType.None)
            {
                return new EmptyResult();
            }

            try
            {
                application.DeveloperSiteState = (int)DeveloperSiteState.Creating;
                db.SaveChanges();

                string developerSiteUrl;
                if (_siteManager.TryCreateDeveloperSite(slug, out developerSiteUrl))
                {
                    // Clone the repository to the developer site
                    var devRepositoryManager = new RemoteRepositoryManager(application.ServiceUrl + "dev/scm");
                    devRepositoryManager.Credentials = _credentialProvider.GetCredentials();
                    devRepositoryManager.CloneRepository(repositoryType);

                    application.DeveloperSiteUrl = developerSiteUrl;
                    db.SaveChanges();

                    return Json(developerSiteUrl);
                }
            }
            catch
            {
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
        public ActionResult Delete(string slug)
        {
            Application application = db.Applications.SingleOrDefault(a => a.Slug == slug);
            if (application != null)
            {
                try
                {
                    IRepositoryManager repositoryManager = GetRepositoryManager(application);
                    repositoryManager.Delete();
                }
                catch
                {
                }

                _siteManager.DeleteSite(slug);

                db.Applications.Remove(application);
                db.SaveChanges();

                return RedirectToAction("Index");
            }

            return HttpNotFound();
        }

        protected override void Dispose(bool disposing)
        {
            db.Dispose();
            base.Dispose(disposing);
        }

        private IRepositoryManager GetRepositoryManager(Application application)
        {
            var repositoryManager = new RemoteRepositoryManager(application.ServiceUrl + "live/scm");
            repositoryManager.Credentials = _credentialProvider.GetCredentials();
            return repositoryManager;
        }
    }
}