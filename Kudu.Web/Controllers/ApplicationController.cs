using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using Kudu.Client.Infrastructure;
using Kudu.SiteManagement;
using Kudu.SiteManagement.Certificates;
using Kudu.SiteManagement.Configuration;
using Kudu.SiteManagement.Configuration.Section;
using Kudu.SiteManagement.Context;
using Kudu.Web.Infrastructure;
using Kudu.Web.Models;

namespace Kudu.Web.Controllers
{
    public class ApplicationController : Controller
    {
        private readonly IApplicationService _applicationService;
        private readonly KuduEnvironment _environment;
        private readonly IKuduContext _context;
        private readonly ICertificateSearcher _certificates;
        private readonly ICredentialProvider _credentialProvider;

        public ApplicationController(IApplicationService applicationService,
                                     ICredentialProvider credentialProvider,
                                     KuduEnvironment environment,
                                     IKuduContext context, 
                                     ICertificateSearcher certificates)
        {
            _applicationService = applicationService;
            _credentialProvider = credentialProvider;
            _environment = environment;
            _context = context;
            _certificates = certificates;
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
            return GetApplicationView("settings", "Details", slug);
        }

        [HttpGet]
        public ActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public async Task<ActionResult> Create(string name)
        {
            string slug = name.GenerateSlug();

            try
            {
                await _applicationService.AddApplication(slug);

                return RedirectToAction("Details", new { slug });
            }
            catch (SiteExistsException)
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
        public async Task<ActionResult> Delete(string slug)
        {
            if (await _applicationService.DeleteApplication(slug))
            {
                return RedirectToAction("Index");
            }

            return HttpNotFound();
        }

        [HttpPost]
        [ActionName("add-custom-site-binding")]
        public async Task<ActionResult> AddCustomSiteBinding(string slug, string siteSchema, string siteIp, string sitePort, string siteHost, string siteRequireSni, string siteCertificate)
        {
            IApplication application = _applicationService.GetApplication(slug);

            if (application == null)
            {
                return HttpNotFound();
            }

            _applicationService.AddSiteBinding(slug, new KuduBinding {
                Schema = siteSchema.Equals("https://", StringComparison.OrdinalIgnoreCase) ? UriScheme.Https : UriScheme.Http,
                Ip = siteIp,
                Port = int.Parse(sitePort),
                Host = siteHost,
                Sni = bool.Parse(siteRequireSni),
                Certificate = siteCertificate,
                SiteType = SiteType.Live
            });

            return await GetApplicationView("settings", "Details", slug);
        }

        [HttpPost]
        [ActionName("remove-custom-site-binding")]
        public async Task<ActionResult> RemoveCustomSiteBinding(string slug, string siteBinding)
        {
            IApplication application = _applicationService.GetApplication(slug);

            if (application == null)
            {
                return HttpNotFound();
            }

            _applicationService.RemoveLiveSiteBinding(slug, siteBinding);

            return await GetApplicationView("settings", "Details", slug);
        }

        [HttpPost]
        [ActionName("add-service-site-binding")]
        public async Task<ActionResult> AddServiceSiteBinding(string slug, string siteSchema, string siteIp, string sitePort, string siteHost, string siteRequireSni, string siteCertificate)
        {
            IApplication application = _applicationService.GetApplication(slug);

            if (application == null)
            {
                return HttpNotFound();
            }

            _applicationService.AddSiteBinding(slug, new KuduBinding {
                Schema = siteSchema.Equals("https://", StringComparison.OrdinalIgnoreCase) ? UriScheme.Https : UriScheme.Http,
                Ip = siteIp,
                Port = int.Parse(sitePort),
                Host = siteHost,
                Sni = bool.Parse(siteRequireSni),
                Certificate = siteCertificate,
                SiteType = SiteType.Service
            });

            return await GetApplicationView("settings", "Details", slug);
        }

        [HttpPost]
        [ActionName("remove-service-site-binding")]
        public async Task<ActionResult> RemoveServiceSiteBinding(string slug, string siteBinding)
        {
            IApplication application = _applicationService.GetApplication(slug);

            if (application == null)
            {
                return HttpNotFound();
            }

            _applicationService.RemoveServiceSiteBinding(slug, siteBinding);

            return await GetApplicationView("settings", "Details", slug);
        }

        private async Task<ActionResult> GetApplicationView(string tab, string viewName, string slug)
        {
            var application = _applicationService.GetApplication(slug);

            ICredentials credentials = _credentialProvider.GetCredentials();
            var repositoryInfo = await application.GetRepositoryInfo(credentials);
            var appViewModel = new ApplicationViewModel(application, _context, _certificates.FindAll());
            appViewModel.RepositoryInfo = repositoryInfo;

            ViewBag.slug = slug;
            ViewBag.tab = tab;
            ViewBag.appName = appViewModel.Name;
            ViewBag.siteBinding = String.Empty;

            ModelState.Clear();

            return View(viewName, appViewModel);
        }
    }
}