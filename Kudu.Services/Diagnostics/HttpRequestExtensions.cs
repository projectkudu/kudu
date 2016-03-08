using System.Web;

namespace Kudu.Services.Diagnostics
{
    public static class HttpRequestExtensions
    {
        public static bool IsFunctionsPortalRequest(this HttpRequest request)
        {
            return request.Headers[Constants.FunctionsPortal] != null;
        }
    }
}
