using System;
using System.Diagnostics;

#if NET || NETCOREAPP
using Microsoft.AspNetCore.Http;
#else
using System.Web;
using System.Web.Hosting;
#endif

namespace Kudu.Core
{
    /// <summary>
    /// Temporary helper class for retrieving the current <see cref="HttpContext"/> . This temporary
    /// workaround should be removed in the future and <see cref="HttpContext"/> should be retrieved
    /// from the current controller, middleware, or page instead.
#if NET || NETCOREAPP
    ///
    /// If working in another component, the current <see cref="HttpContext"/> can be retrieved from an <see cref="IHttpContextAccessor"/>
    /// retrieved via dependency injection.
#endif
    /// </summary>
    internal static class HttpContextHelper
    {
        private const string Message = "Prefer accessing HttpContext via injection";

        /// <summary>
        /// Gets the current <see cref="HttpContext"/>. Returns <c>null</c> if there is no current <see cref="HttpContext"/>.
        /// </summary>
#if NET || NETCOREAPP
#if NET5_0_OR_GREATER
        [Obsolete(Message, error: false, DiagnosticId = "HttpContextCurrent", UrlFormat = "https://docs.microsoft.com/en-us/aspnet/core/fundamentals/http-context")]
#else
        [Obsolete(Message, error: false)]
#endif
        public static HttpContext Current => HttpContextAccessor.HttpContext;

        private static readonly HttpContextAccessor HttpContextAccessor = new HttpContextAccessor();
#else
#pragma warning disable UA0005 // Do not use HttpContext.Current
        [Obsolete(Message, error: false)]
        public static HttpContext Current => HttpContext.Current;
#pragma warning restore UA0005 // Do not use HttpContext.Current
#endif
    }
}
