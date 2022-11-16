#if NET6_0_OR_GREATER
using Microsoft.AspNetCore.Http;
#else
using System.Web;
#endif

namespace Kudu.Core
{  
    internal static class HttpContextHelper
    {
#if NET6_0_OR_GREATER
        public static HttpContext Current => HttpContextAccessor.HttpContext;

        private static readonly HttpContextAccessor HttpContextAccessor = new HttpContextAccessor();
#else
        public static HttpContext Current => HttpContext.Current;
#endif
    }
}
