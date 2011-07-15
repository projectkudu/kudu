using System;
using System.Web.Mvc;

namespace Kudu.Services {
    [AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = false)]
    public sealed class FormattedExceptionFilterAttribute : FilterAttribute, IExceptionFilter {
        public void OnException(ExceptionContext filterContext) {
            filterContext.ExceptionHandled = true;
            filterContext.HttpContext.Response.TrySkipIisCustomErrors = true;
            filterContext.HttpContext.ClearError();
            filterContext.HttpContext.Response.StatusCode = 500;
            filterContext.Result = new ContentResult {
                Content = filterContext.Exception.Message
            };
        }
    }
}