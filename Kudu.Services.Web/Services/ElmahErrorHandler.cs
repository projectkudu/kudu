using System;
using System.Net.Http;
using Elmah;
using Microsoft.ApplicationServer.Http.Dispatcher;

namespace Kudu.Services.Web.Services
{
    public class ElmahErrorHandler : HttpErrorHandler
    {
        protected override bool OnTryProvideResponse(Exception exception, ref HttpResponseMessage message)
        {
            ErrorLog.GetDefault(null).Log(new Error(exception));
            return false;
        }
    }
}