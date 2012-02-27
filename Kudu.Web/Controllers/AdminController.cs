using System.Web.Mvc;
using Kudu.SiteManagement;
using Kudu.Web.Infrastructure;
using Kudu.Web.Models;

namespace Kudu.Web.Controllers
{
    public class AdminController : Controller
    {
        private readonly IPathResolver _pathResolver;
        private readonly KuduEnvironment _env;
        public AdminController(IPathResolver pathResolver, KuduEnvironment env)
        {
            _pathResolver = pathResolver;
            _env = env;
        }
        //
        // GET: /Admin/

        public ActionResult Index()
        {
            ViewBag.showAdminWarning = !_env.IsAdmin && _env.RunningAgainstLocalKuduService;

            var model = new AdminSettingsViewModel
            {
                ServiceSitePath = _pathResolver.ServiceSitePath,
                SitesPath = _pathResolver.SitesPath
            };

            return View(model);
        }

    }
}
