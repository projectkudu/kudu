using System;
using System.Web.Mvc;

namespace Kudu.Services.Controllers {
    [AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = false)]
    public sealed class JsonExceptionFilterAttribute : FilterAttribute, IExceptionFilter {
        public void OnException(ExceptionContext filterContext) {
            filterContext.ExceptionHandled = true;
            filterContext.HttpContext.Response.TrySkipIisCustomErrors = true;
            filterContext.HttpContext.ClearError();
            filterContext.HttpContext.Response.StatusCode = 500;
            filterContext.Result = new JsonResult {
                JsonRequestBehavior = JsonRequestBehavior.AllowGet,
                Data = filterContext.Exception.Message
            };
        }
    }
}