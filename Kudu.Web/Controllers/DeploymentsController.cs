using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using Kudu.Client.Deployment;
using Kudu.Client.Infrastructure;
using Kudu.Contracts.Infrastructure;
using Kudu.SiteManagement;
using Kudu.Web.Infrastructure;
using Kudu.Web.Models;
using Mvc.Async;

namespace Kudu.Web.Controllers
{
    public class DeploymentsController : TaskAsyncController
    {
        private readonly IApplicationService _applicationService;
        private readonly ICredentialProvider _credentialProvider;
        private readonly ISiteManager _siteManager;

        public DeploymentsController(IApplicationService applicationService,
                                     ICredentialProvider credentialProvider, 
                                     ISiteManager siteManager)
        {
            _applicationService = applicationService;
            _credentialProvider = credentialProvider;
            _siteManager = siteManager;
        }

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            ViewBag.tab = "deployments";

            base.OnActionExecuting(filterContext);
        }

        public Task<ActionResult> Index(string slug)
        {
            var site = _siteManager.GetSite(slug);

            if (site == null)
            {
                return HttpNotFoundAsync();
            }

            ICredentials credentials = _credentialProvider.GetCredentials();
            RemoteDeploymentManager deploymentManager = site.GetDeploymentManager(credentials);

            // TODO: Do this in parallel
            return deploymentManager.GetResultsAsync().Then(results =>
            {
                return site.GetRepositoryInfo(credentials).Then(repositoryInfo =>
                {
                    var appViewModel = new ApplicationViewModel(slug, site);
                    appViewModel.RepositoryInfo = repositoryInfo;
                    appViewModel.Deployments = results.ToList();

                    ViewBag.slug = slug;
                    ViewBag.appName = appViewModel.Name;

                    return (ActionResult)View(appViewModel);
                });
            });

        }

        public Task<ActionResult> Deploy(string slug, string id, bool? clean)
        {
            var site = _siteManager.GetSite(slug);

            if (site == null)
            {
                return HttpNotFoundAsync();
            }

            ICredentials credentials = _credentialProvider.GetCredentials();
            RemoteDeploymentManager deploymentManager = site.GetDeploymentManager(credentials);

            return deploymentManager.DeployAsync(id, clean ?? false)
                                    .ContinueWith(task =>
                                    {
                                        return (ActionResult)RedirectToAction("Index", new { slug });
                                    });
        }

        public Task<ActionResult> Log(string slug, string id)
        {
            var site = _siteManager.GetSite(slug);

            if (site == null)
            {
                return HttpNotFoundAsync();
            }

            ICredentials credentials = _credentialProvider.GetCredentials();
            RemoteDeploymentManager deploymentManager = site.GetDeploymentManager(credentials);

            return deploymentManager.GetLogEntriesAsync(id).Then(entries =>
            {
                ViewBag.slug = slug;
                ViewBag.appName = slug;
                ViewBag.id = id;

                return (ActionResult)View(entries);
            });
        }

        public Task<ActionResult> Details(string slug, string id, string logId)
        {
            var site = _siteManager.GetSite(slug);
            if (site == null)
            {
                return HttpNotFoundAsync();
            }

            ICredentials credentials = _credentialProvider.GetCredentials();
            RemoteDeploymentManager deploymentManager = site.GetDeploymentManager(credentials);

            return deploymentManager.GetLogEntryDetailsAsync(id, logId).Then(entries =>
            {
                ViewBag.slug = slug;
                ViewBag.appName = slug;
                ViewBag.id = id;
                ViewBag.verbose = true;

                return (ActionResult)View("Log", entries);
            });
        }
    }
}
