using System.Web.Mvc;
using Kudu.Web.Infrastructure;

namespace Kudu.Web.Controllers
{
    public class AdminController : Controller
    {
        private readonly KuduEnvironment _environment;

        public AdminController(KuduEnvironment environment)
        {
            _environment = environment;
        }

        public ActionResult Index()
        {
            return View(_environment);
        }

    }
}
