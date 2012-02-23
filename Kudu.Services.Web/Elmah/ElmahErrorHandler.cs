using System;
using System.Net.Http;
using System.Web;
using Elmah;
using Microsoft.ApplicationServer.Http.Dispatcher;

namespace Kudu.Services.Web.Elmah
{
    public class ElmahErrorHandler : HttpErrorHandler
    {
        protected override bool OnTryProvideResponse(Exception exception, ref HttpResponseMessage message)
        {
            var context = HttpContext.Current;
            ErrorLog log = null;

            if (context != null)
            {
                log = ErrorLog.GetDefault(context);
            }
            else
            {
                log = ErrorLog.GetDefault(null);
            }

            log.Log(new Error(exception));
            return false;
        }
    }
}