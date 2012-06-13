using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using Kudu.Client.Infrastructure;
using Kudu.Contracts.Infrastructure;
using Kudu.Web.Infrastructure;
using Kudu.Web.Models;
using Mvc.Async;

namespace Kudu.Web.Controllers
{
    public class ApplicationController : TaskAsyncController
    {
        private readonly IApplicationService _applicationService;
        private readonly KuduEnvironment _environment;
        private readonly ICredentialProvider _credentialProvider;

        public ApplicationController(IApplicationService applicationService,
                                     ICredentialProvider credentialProvider,
                                     KuduEnvironment environment)
        {
            _applicationService = applicationService;
            _credentialProvider = credentialProvider;
            _environment = environment;
        }

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            ViewBag.showAdmingWarning = !_environment.IsAdmin && _environment.RunningAgainstLocalKuduService;
            base.OnActionExecuting(filterContext);
        }

        public ViewResult Index()
        {
            var applications = (from name in _applicationService.GetApplications()
                                orderby name
                                select name).ToList();

            return View(applications);
        }

        public Task<ActionResult> Details(string slug)
        {
            var site = _applicationService.GetSite(slug);

            if (site == null)
            {
                return HttpNotFoundAsync();
            }

            ICredentials credentials = _credentialProvider.GetCredentials();
            return site.GetRepositoryInfo(credentials).Then(repositoryInfo =>
            {
                var appViewModel = new ApplicationViewModel(slug, site);
                appViewModel.RepositoryInfo = repositoryInfo;

                ViewBag.slug = slug;
                ViewBag.tab = "settings";
                ViewBag.appName = appViewModel.Name;

                return (ActionResult)View(appViewModel);
            });
        }

        [HttpGet]
        public ActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public ActionResult Create(string name, string hostname = null, int port = 0)
        {
            string slug = name.GenerateSlug();

            try
            {
                _applicationService.AddApplication(slug, hostname, port);

                return RedirectToAction("Details", new { slug });
            }
            catch (SiteExistsFoundException)
            {
                ModelState.AddModelError("Name", "Site already exists");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
            }

            return View("Create");
        }

        [HttpPost]
        public ActionResult Delete(string slug)
        {
            if (_applicationService.DeleteApplication(slug))
            {
                return RedirectToAction("Index");
            }

            return HttpNotFound();
        }

        public Task<ActionResult> Trace(string slug)
        {
            var site = _applicationService.GetSite(slug);
            IApplication application = _applicationService.GetApplication(slug);

            if (application == null || site == null)
            {
                return HttpNotFoundAsync();
            }

            ICredentials credentials = _credentialProvider.GetCredentials();
            return site.DownloadTrace(credentials).Then(document =>
            {
                return (ActionResult)View(document);
            });
        }

        public ActionResult Develop(string slug)
        {
            _applicationService.CreateDevelopmentSite(slug);

            return RedirectToAction("Details", new { slug });
        }
    }
}