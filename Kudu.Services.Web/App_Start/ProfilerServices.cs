using System;
using System.Web;
using Kudu.Contracts;
using Microsoft.Web.Infrastructure.DynamicModuleHelper;

[assembly: WebActivator.PreApplicationStartMethod(
    typeof(Kudu.Services.Web.App_Start.ProfilerServices), "Initialize")]

namespace Kudu.Services.Web.App_Start
{
    public static class ProfilerServices
    {
        private static readonly object _profilerKey = new object();
        private static Func<IProfiler> _profilerFactory;

        internal static bool Enabled
        {
            get
            {
                return AppSettings.ProfilingEnabled;
            }
        }

        internal static IProfiler CurrentRequestProfiler
        {
            get
            {
                var httpContext = HttpContext.Current;
                if (httpContext == null)
                {
                    return null;
                }

                return GetRequestProfiler(httpContext);
            }
        }

        public static void SetProfilerFactory(Func<IProfiler> profilerFactory)
        {
            _profilerFactory = profilerFactory;
        }

        internal static IProfiler GetRequestProfiler(HttpContext httpContext)
        {
            return httpContext.Items[_profilerKey] as IProfiler;
        }

        internal static IProfiler CreateRequestProfiler(HttpContext httpContext)
        {
            var profiler = (IProfiler)httpContext.Items[_profilerKey];
            if (profiler == null)
            {
                profiler = _profilerFactory();
                httpContext.Items[_profilerKey] = profiler;
            }

            return profiler;
        }

        public static void Initialize()
        {
            if (ProfilerServices.Enabled)
            {
                DynamicModuleUtility.RegisterModule(typeof(ProfilerModule));
            }
        }
    }

    public class ProfilerModule : IHttpModule
    {
        private static readonly object _stepKey = new object();

        public void Init(HttpApplication context)
        {
            context.BeginRequest += (sender, e) =>
            {
                var httpContext = ((HttpApplication)sender).Context;

                // Skip favicon.ico
                if (httpContext.Request.RawUrl.EndsWith("favicon.ico", StringComparison.OrdinalIgnoreCase) ||
                    httpContext.Request.Path.StartsWith("/profiler", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                var profiler = ProfilerServices.CreateRequestProfiler(httpContext);

                httpContext.Items[_stepKey] = profiler.Step(httpContext.Request.RawUrl);
            };

            context.EndRequest += (sender, e) =>
            {
                var httpContext = ((HttpApplication)sender).Context;

                var requestStep = (IDisposable)httpContext.Items[_stepKey];

                if (requestStep != null)
                {
                    requestStep.Dispose();
                }
            };
        }

        public void Dispose() { }
    }
}

