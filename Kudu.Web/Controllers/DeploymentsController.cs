using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using Kudu.Client;
using Kudu.Client.Deployment;
using Kudu.Client.Infrastructure;
using Kudu.Core.Deployment;
using Kudu.Core.SourceControl;
using Kudu.SiteManagement;
using Kudu.SiteManagement.Certificates;
using Kudu.SiteManagement.Configuration;
using Kudu.SiteManagement.Context;
using Kudu.Web.Infrastructure;
using Kudu.Web.Models;

namespace Kudu.Web.Controllers
{
    public class DeploymentsController : Controller
    {
        private readonly IApplicationService _applicationService;
        private readonly ICredentialProvider _credentialProvider;
        private readonly IKuduContext _context;
        private readonly ICertificateSearcher _certificates;

        public DeploymentsController(IApplicationService applicationService,
                                     ICredentialProvider credentialProvider,
                                     IKuduContext context,
                                     ICertificateSearcher certificates)
        {
            _applicationService = applicationService;
            _credentialProvider = credentialProvider;
            _context = context;
            _certificates = certificates;
        }

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            ViewBag.tab = "deployments";

            base.OnActionExecuting(filterContext);
        }

        public async Task<ActionResult> Index(string slug)
        {
            IApplication application = _applicationService.GetApplication(slug);

            if (application == null)
            {
                return HttpNotFound();
            }

            ICredentials credentials = _credentialProvider.GetCredentials();
            RemoteDeploymentManager deploymentManager = application.GetDeploymentManager(credentials);

            Task<IEnumerable<DeployResult>> deployResults = deploymentManager.GetResultsAsync();
            Task<RepositoryInfo> repositoryInfo = application.GetRepositoryInfo(credentials);

            await Task.WhenAll(deployResults, repositoryInfo);

            var appViewModel = new ApplicationViewModel(application, _context, _certificates.FindAll())
            {
                RepositoryInfo = repositoryInfo.Result,
                Deployments = deployResults.Result.ToList()
            };

            ViewBag.slug = slug;
            ViewBag.appName = appViewModel.Name;

            return View(appViewModel);
        }

        [HttpPost]
        public async Task<ActionResult> TriggerFetch(string slug, string repositoryUrl, RepositoryType repositoryType)
        {
            IApplication application = _applicationService.GetApplication(slug);

            if (application == null)
            {
                return HttpNotFound();
            }

            ICredentials credentials = _credentialProvider.GetCredentials();
            RemoteFetchManager fetchManager = application.GetFetchManager(credentials);

            try
            {
                await fetchManager.TriggerFetch(repositoryUrl, repositoryType);
            }
            catch (HttpUnsuccessfulRequestException)
            {
                // Ignore any failures in triggering the deployment
            }
            return new EmptyResult();
        }

        public async Task<ActionResult> Deploy(string slug, string id, bool? clean)
        {
            IApplication application = _applicationService.GetApplication(slug);

            if (application == null)
            {
                return HttpNotFound();
            }

            ICredentials credentials = _credentialProvider.GetCredentials();
            RemoteDeploymentManager deploymentManager = application.GetDeploymentManager(credentials);

            await deploymentManager.DeployAsync(id, clean: clean ?? false);
            return RedirectToAction("Index", new { slug });
        }

        public async Task<ActionResult> Log(string slug, string id)
        {
            IApplication application = _applicationService.GetApplication(slug);

            if (application == null)
            {
                return HttpNotFound();
            }

            ICredentials credentials = _credentialProvider.GetCredentials();
            RemoteDeploymentManager deploymentManager = application.GetDeploymentManager(credentials);

            IEnumerable<LogEntry> entries = await deploymentManager.GetLogEntriesAsync(id);

            ViewBag.slug = slug;
            ViewBag.appName = application.Name;
            ViewBag.id = id;
            return View(entries);
        }

        public async Task<ActionResult> Details(string slug, string id, string logId)
        {
            IApplication application = _applicationService.GetApplication(slug);
            if (application == null)
            {
                return HttpNotFound();
            }

            ICredentials credentials = _credentialProvider.GetCredentials();
            RemoteDeploymentManager deploymentManager = application.GetDeploymentManager(credentials);
            IEnumerable<LogEntry> entries = await deploymentManager.GetLogEntryDetailsAsync(id, logId);
            
            ViewBag.slug = slug;
            ViewBag.appName = application.Name;
            ViewBag.id = id;
            ViewBag.verbose = true;

            return View("Log", entries);
        }
    }
}
