using System.Web.Mvc;
using System.Xml.Linq;

namespace Kudu.Web.Controllers
{
    public class AnalysisController : Controller
    {
        [HttpGet]
        public ActionResult Trace()
        {
            return View();
        }

        [HttpPost]
        [ActionName("trace")]
        public ActionResult PerformTrace()
        {
            var document = XDocument.Load(Request.Files[0].InputStream);
            return View(document);
        }
    }
}
