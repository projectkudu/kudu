using System.Web;
using Microsoft.Web.Infrastructure.DynamicModuleHelper;
using MvcMiniProfiler;

[assembly: WebActivator.PreApplicationStartMethod(
    typeof(Kudu.Services.Web.App_Start.MiniProfilerPackage), "PreStart")]

namespace Kudu.Services.Web.App_Start
{
    public static class MiniProfilerPackage
    {
        public static void PreStart()
        {
            // Be sure to restart you ASP.NET Developement server, this code will not run until you do that. 
            //Make sure the MiniProfiler handles BeginRequest and EndRequest
            DynamicModuleUtility.RegisterModule(typeof(MiniProfilerStartupModule));
        }
    }

    public class MiniProfilerStartupModule : IHttpModule
    {
        public void Init(HttpApplication context)
        {
            context.BeginRequest += (sender, e) =>
            {
                var request = ((HttpApplication)sender).Request;
                //TODO: By default only local requests are profiled, optionally you can set it up
                //  so authenticated users are always profiled
                if (request.IsAuthenticated || request.IsLocal) { MiniProfiler.Start(); }
            };


            // TODO: You can control who sees the profiling information
            /*
            context.AuthenticateRequest += (sender, e) =>
            {
                if (!CurrentUserIsAllowedToSeeProfiler())
                {
                    MvcMiniProfiler.MiniProfiler.Stop(discardResults: true);
                }
            };
            */

            context.EndRequest += (sender, e) =>
            {
                MiniProfiler.Stop();
            };
        }

        public void Dispose() { }
    }
}

