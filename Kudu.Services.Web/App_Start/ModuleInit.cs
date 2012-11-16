using Kudu.Services.Web.Security;
using Kudu.Services.Web.Tracing;
using Microsoft.Web.Infrastructure.DynamicModuleHelper;

[assembly: WebActivator.PreApplicationStartMethod(
    typeof(Kudu.Services.Web.App_Start.ModuleInit), "Initialize")]


namespace Kudu.Services.Web.App_Start
{
    public static class ModuleInit
    {
        public static void Initialize()
        {
            if (AppSettings.BlockLocalhost)
            {
                DynamicModuleUtility.RegisterModule(typeof(BlockLocalhostModule));
            }

            DynamicModuleUtility.RegisterModule(typeof(TraceModule));
        }
    }
}