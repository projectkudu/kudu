using System.IO;
using Kudu.ContainerServices.Agent.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Kudu.ContainerServices.Agent
{
    public class Startup
    {
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            // Use basic authentication to validate user requests
            app.UseMiddleware<BasicAuthHelper>();

            app.UseRouting()
               .UseEndpoints(endpoints =>
               {
                   // Map attribute routed controllers.
                   endpoints.MapControllers();
               });
            
            
        }
    }
}
