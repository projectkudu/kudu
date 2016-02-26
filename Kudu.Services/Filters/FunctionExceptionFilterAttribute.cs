using Kudu.Services.Arm;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Web.Http.Filters;

namespace Kudu.Services.Filters
{
    public class FunctionExceptionFilterAttribute : ExceptionFilterAttribute
    {
        public override void OnException(HttpActionExecutedContext context)
        {
            var statusCode = HttpStatusCode.InternalServerError;

            if (context.Exception is FileNotFoundException)
            {
                statusCode = HttpStatusCode.NotFound;
            }
            else if (context.Exception is InvalidOperationException)
            {
                statusCode = HttpStatusCode.Conflict;
            }
            else if (context.Exception is ArgumentException)
            {
                statusCode = HttpStatusCode.BadRequest;
            }

            context.Response =  ArmUtils.CreateErrorResponse(context.Request, statusCode, context.Exception);
        }
    }
}
