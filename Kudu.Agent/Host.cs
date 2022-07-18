// ------------------------------------------------------------------------------
//  <copyright file="Host.cs" company="Microsoft">
//      Copyright (c) Microsoft Corporation.  All rights reserved.
//  </copyright>
// ------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Kudu.Agent
{
    public static class Host
    {
        public static void Run(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder
                        .ConfigureServices(services =>
                        {
                            services.AddControllers();
                        })
                        .UseKestrel(options =>
                        {
                            options.ListenAnyIP(port: 50555);
                        })
                        .UseStartup<Startup>();
                });

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                Exception exception = e.ExceptionObject as Exception;
                if (exception != null)
                {
                    Trace.TraceError("Unhandled Exception. Error: " + exception.ToString());
                }
            }
            finally
            {
                if (!e.IsTerminating)
                {
                    Exception exception = e.ExceptionObject as Exception;
                    if (exception != null)
                    {
                        int errorCode = Marshal.GetHRForException(exception);
                        Environment.Exit(errorCode);
                    }
                }
            }
        }
    }
}
