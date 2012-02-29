using System.Web.Mvc;
using Kudu.SiteManagement;
using Kudu.Web.Infrastructure;
using Kudu.Web.Models;

namespace Kudu.Web.Controllers
{
    public class AdminController : Controller
    {
        private readonly IPathResolver _pathResolver;
        private readonly KuduEnvironment _environment;

        public AdminController(IPathResolver pathResolver, KuduEnvironment environment)
        {
            _pathResolver = pathResolver;
            _environment = environment;
        }

        public ActionResult Index()
        {
            var model = new AdminSettingsViewModel
            {
                ServiceSitePath = _pathResolver.ServiceSitePath,
                SitesPath = _pathResolver.SitesPath
            };

            return View(model);
        }

    }
}
