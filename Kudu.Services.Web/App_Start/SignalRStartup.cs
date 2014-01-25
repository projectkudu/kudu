using System;
using System.Threading.Tasks;
using Microsoft.Owin;
using Owin;

[assembly: OwinStartup(typeof(Kudu.Services.Web.App_Start.SignalRStartup))]

namespace Kudu.Services.Web.App_Start
{
    public static class SignalRStartup
    {
        public static void Configuration(IAppBuilder app)
        {
            app.MapSignalR<PersistentCommandController>("/commandstream");
        }
    }
}
