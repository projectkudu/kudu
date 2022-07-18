// ------------------------------------------------------------------------------
//  <copyright file="Startup.cs" company="Microsoft">
//      Copyright (c) Microsoft Corporation.  All rights reserved.
//  </copyright>
// ------------------------------------------------------------------------------

using System.IO;
using Kudu.Agent.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Kudu.Agent
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
                   // Map custom route for canary inside main container.
                   endpoints.MapGet("/mscanary471987.php", async context =>
                   {
                       await context.Response.WriteAsync("result=success");
                   });
               });
            
            
        }
    }
}
