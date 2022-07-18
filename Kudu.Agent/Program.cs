// ------------------------------------------------------------------------------
//  <copyright file="Program.cs" company="Microsoft">
//      Copyright (c) Microsoft Corporation.  All rights reserved.
//  </copyright>
// ------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace Kudu.Agent
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            Trace.AutoFlush = true;
            Trace.WriteLine("Kudu Agent");

            // StdOut/StdErr is piped by the ContainerProcess class in DWASSVC
            Trace.Listeners.Add(new ConsoleTraceListener());
            Host.Run(args);
        }
    }
}
