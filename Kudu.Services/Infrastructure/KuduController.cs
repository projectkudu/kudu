using System;
using System.Text;
using System.Web.Mvc;

namespace Kudu.Services.Infrastructure {
    public class KuduController : Controller {
        protected override IActionInvoker CreateActionInvoker() {
            return new KuduActionInvoker();
        }

        protected override JsonResult Json(object data, string contentType, Encoding contentEncoding, JsonRequestBehavior behavior) {
            return new KuduJsonResult {
                Data = data,
                ContentType = contentType,
                ContentEncoding = contentEncoding,
                JsonRequestBehavior = behavior,
                MaxJsonLength = Int32.MaxValue
            };
        }
    }
}
