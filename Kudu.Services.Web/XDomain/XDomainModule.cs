using System;
using System.Web;

namespace Kudu.Services.Web.XDomain
{
    public class XDomainModule : IHttpModule
    {
        public void Dispose()
        {
        }

        public void Init(HttpApplication application)
        {
            application.BeginRequest += Application_BeginRequest; 
        }

        void Application_BeginRequest(object sender, EventArgs e)
        {
            var application = (HttpApplication)sender;
            var context = new HttpContextWrapper(application.Context);
            if (!CheckCrossSiteRequestOrigin(context))
            {
                context.Response.End();
                context.ApplicationInstance.CompleteRequest();
                return;
            }

            if (String.Equals("OPTIONS", context.Request.HttpMethod, StringComparison.OrdinalIgnoreCase))
            {
                SetAllowCrossSiteRequestHeaders(context);
                context.Response.End();
                context.ApplicationInstance.CompleteRequest();
                return;
            }
        }

        public static void SetAllowCrossSiteRequestHeaders(HttpContextBase context)
        {
            // this directs the agent not to cache this header
            // since we don't return Access-Control-Max-Age, it never caches
            //context.Response.Cache.SetExpires(DateTime.UtcNow.AddDays(-1));
            //context.Response.Cache.SetValidUntilExpires(false);
            //context.Response.Cache.SetRevalidation(HttpCacheRevalidation.AllCaches);
            //context.Response.Cache.SetCacheability(HttpCacheability.NoCache);
            //context.Response.Cache.SetNoStore();

            context.Response.AppendHeader("Access-Control-Allow-Methods", "GET,POST,PUT,DELETE,OPTIONS,HEAD");

            string requestHeaders = context.Request.Headers["Access-Control-Request-Headers"];
            if (!String.IsNullOrEmpty(requestHeaders))
            {
                context.Response.AppendHeader("Access-Control-Allow-Headers", requestHeaders);
            }
        }

        public static bool CheckCrossSiteRequestOrigin(HttpContextBase context)
        {
            string origin = context.Request.Headers["Origin"];
            bool allowed = String.IsNullOrEmpty(origin);
            if (!allowed)
            {
                // some policy 
                allowed = true; // Regex.IsMatch(origin, "http://sso[^./]+[.]kudu1[.]antares-test[.]windows-int[.]net/?"); 
                if (allowed)
                {
                    context.Response.AppendHeader("Access-Control-Allow-Origin", origin);

                    // allowing Cookie for LiveId auth
                    // xhr.withCredentials = true
                    context.Response.AppendHeader("Access-Control-Allow-Credentials", "true");
                }
            }

            return allowed;
        }
    }
}