using System;
using System.IO;
using System.Web.Mvc;
using System.Xml.Linq;
using Kudu.Web.Infrastructure;

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
            var file = Request.Files[0];
            XDocument document = null;

            if (Path.GetExtension(file.FileName).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                document = ZipHelper.ExtractTrace(file.InputStream);
            }
            else
            {
                document = XDocument.Load(file.InputStream);
            }
            return View(document);
        }
    }
}
