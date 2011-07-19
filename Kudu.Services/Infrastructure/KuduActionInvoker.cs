using System;
using System.Globalization;
using System.Web.Mvc;

namespace Kudu.Services.Infrastructure {
    public class KuduActionInvoker : ControllerActionInvoker {
        protected override ActionResult CreateActionResult(ControllerContext controllerContext, ActionDescriptor actionDescriptor, object actionReturnValue) {
            if (actionReturnValue == null) {
                return new EmptyResult();
            }

            if (actionReturnValue is ActionResult) {
                return actionReturnValue as ActionResult;
            }

            if (actionReturnValue is string) {
                return new ContentResult { Content = Convert.ToString(actionReturnValue, CultureInfo.InvariantCulture) };
            }

            return new KuduJsonResult {
                Data = actionReturnValue,
                JsonRequestBehavior = JsonRequestBehavior.AllowGet,
                MaxJsonLength = Int32.MaxValue
            };
        }
    }
}
