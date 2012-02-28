using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using Kudu.Client.Deployment;
using Kudu.Client.Infrastructure;
using Kudu.Contracts.Infrastructure;
using Kudu.Web.Infrastructure;
using Kudu.Web.Models;
using Mvc.Async;

namespace Kudu.Web.Controllers
{
    public class DeploymentsController : TaskAsyncController
    {
        private readonly KuduContext db = new KuduContext();
        private readonly ICredentialProvider _credentialProvider;

        public DeploymentsController(ICredentialProvider credentialProvider)
        {
            _credentialProvider = credentialProvider;
        }

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            ViewBag.tab = "deployments";

            base.OnActionExecuting(filterContext);
        }

        public Task<ActionResult> Index(string slug)
        {
            Application application = db.Applications.SingleOrDefault(a => a.Slug == slug);
            if (application != null)
            {
                ICredentials credentials = _credentialProvider.GetCredentials();
                RemoteDeploymentManager deploymentManager = application.GetDeploymentManager(credentials);
                
                // TODO: Do this in parallel
                return deploymentManager.GetResultsAsync().Then(results =>
                {
                    return application.GetRepositoryInfo(credentials).Then(repositoryInfo =>
                    {
                        var appViewModel = new ApplicationViewModel(application);
                        appViewModel.RepositoryInfo = repositoryInfo;
                        appViewModel.Deployments = results.ToList();

                        ViewBag.slug = slug;
                        ViewBag.appName = appViewModel.Name;

                        return (ActionResult)View(appViewModel);
                    });
                });
            }

            return Task.Factory.StartNew(() => (ActionResult)HttpNotFound());
        }

        public Task<ActionResult> Deploy(string slug, string id)
        {
            Application application = db.Applications.SingleOrDefault(a => a.Slug == slug);
            if (application != null)
            {
                ICredentials credentials = _credentialProvider.GetCredentials();
                RemoteDeploymentManager deploymentManager = application.GetDeploymentManager(credentials);

                return deploymentManager.DeployAsync(id)
                                        .ContinueWith(task => (ActionResult)RedirectToAction("Index", new { slug = slug }));
            }

            return Task.Factory.StartNew(() => (ActionResult)HttpNotFound());
        }

        public Task<ActionResult> Log(string slug, string id)
        {
            Application application = db.Applications.SingleOrDefault(a => a.Slug == slug);
            if (application != null)
            {
                ICredentials credentials = _credentialProvider.GetCredentials();
                RemoteDeploymentManager deploymentManager = application.GetDeploymentManager(credentials);

                return deploymentManager.GetLogEntriesAsync(id).Then(entries =>
                {
                    ViewBag.slug = slug;
                    ViewBag.appName = application.Name;
                    ViewBag.id = id;

                    return (ActionResult)View(entries);
                });
            }

            return Task.Factory.StartNew(() => (ActionResult)HttpNotFound());
        }

        public Task<ActionResult> Details(string slug, string id, string logId)
        {
            Application application = db.Applications.SingleOrDefault(a => a.Slug == slug);
            if (application != null)
            {
                ICredentials credentials = _credentialProvider.GetCredentials();
                RemoteDeploymentManager deploymentManager = application.GetDeploymentManager(credentials);

                return deploymentManager.GetLogEntryDetailsAsync(id, logId).Then(entries =>
                {
                    ViewBag.slug = slug;
                    ViewBag.appName = application.Name;
                    ViewBag.id = id;
                    ViewBag.verbose = true;

                    return (ActionResult)View("Log", entries);
                });
            }

            return Task.Factory.StartNew(() => (ActionResult)HttpNotFound());
        }
    }
}
