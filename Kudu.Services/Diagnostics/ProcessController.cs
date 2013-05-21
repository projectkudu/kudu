using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web.Http;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Core.Diagnostics;
using Kudu.Core.Infrastructure;

namespace Kudu.Services.Performance
{
    public class ProcessController : ApiController
    {
        private readonly ITracer _tracer;
        private readonly IEnvironment _environment;

        public ProcessController(ITracer tracer, IEnvironment environment)
        {
            _tracer = tracer;
            _environment = environment;
        }

        [HttpGet]
        public HttpResponseMessage GetAllProcesses()
        {
            using (_tracer.Step("ProcessController.GetAllProcesses"))
            {
                var results = Process.GetProcesses().Select(p => GetProcessInfo(p, Request.RequestUri.AbsoluteUri.TrimEnd('/') + '/' + p.Id)).OrderBy(p => p.Name.ToLowerInvariant()).ToList();
                return Request.CreateResponse(HttpStatusCode.OK, results);
            }
        }

        [HttpGet]
        public HttpResponseMessage GetProcess(int id)
        {
            using (_tracer.Step("ProcessController.GetProcess"))
            {
                var process = GetProcessById(id);
                return Request.CreateResponse(HttpStatusCode.OK, GetProcessInfo(process, Request.RequestUri.AbsoluteUri, details: true));
            }
        }

        [HttpDelete]
        public void KillProcess(int id)
        {
            using (_tracer.Step("ProcessController.KillProcess"))
            {
                var process = GetProcessById(id);
                process.Kill(includesChildren: true, tracer: _tracer);
            }
        }

        [HttpGet]
        public HttpResponseMessage MiniDump(int id, int dumpType = 0)
        {
            using (_tracer.Step("ProcessController.MiniDump"))
            {
                var process = GetProcessById(id);

                string dumpFile = Path.Combine(_environment.TempPath, "minidump.dmp");
                FileSystemHelpers.DeleteFileSafe(dumpFile);

                _tracer.Trace("MiniDump pid={0}, name={1}, file={2}", process.Id, process.ProcessName, dumpFile);
                process.MiniDump(dumpFile, (MINIDUMP_TYPE)dumpType);
                _tracer.Trace("MiniDump size={0}", new FileInfo(dumpFile).Length);

                HttpResponseMessage response = Request.CreateResponse();
                response.Content = new StreamContent(File.OpenRead(dumpFile));
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment");
                response.Content.Headers.ContentDisposition.FileName = String.Format("{0}-{1:MM-dd-H:mm:ss}.dmp", process.ProcessName, DateTime.UtcNow);
                return response;
            }
        }

        [SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity", Justification = "")]
        private ProcessInfo GetProcessInfo(Process process, string href, bool details = false)
        {
            href = href.TrimEnd('/');
            if (href.EndsWith("/0", StringComparison.OrdinalIgnoreCase))
            {
                href = href.Substring(0, href.Length - 1) + process.Id;
            }

            var selfLink = new Uri(href);
            var info = new ProcessInfo
            {
                Id = process.Id,
                Name = process.ProcessName,
                Href = selfLink
            };

            if (details)
            {
                // this could fail access denied
                info.HandleCount = SafeGetValue(() => process.HandleCount, -1);
                info.ThreadCount = SafeGetValue(() => process.Threads.Count, -1);
                info.ModuleCount = SafeGetValue(() => process.Modules.Count, -1);
                info.FileName = SafeGetValue(() => process.MainModule.FileName, "N/A");

                // always return empty
                //info.Arguments = SafeGetValue(() => process.StartInfo.Arguments, "N/A");
                //info.UserName = SafeGetValue(() => process.StartInfo.UserName, "N/A");

                info.StartTime = SafeGetValue(() => process.StartTime.ToUniversalTime(), DateTime.MinValue);
                info.TotalProcessorTime = SafeGetValue(() => process.TotalProcessorTime, TimeSpan.FromSeconds(-1));
                info.UserProcessorTime = SafeGetValue(() => process.UserProcessorTime, TimeSpan.FromSeconds(-1));
                info.PrivilegedProcessorTime = SafeGetValue(() => process.PrivilegedProcessorTime, TimeSpan.FromSeconds(-1));

                info.PagedSystemMemorySize64 = SafeGetValue(() => process.PagedSystemMemorySize64, -1);
                info.NonpagedSystemMemorySize64 = SafeGetValue(() => process.NonpagedSystemMemorySize64, -1);
                info.PagedMemorySize64 = SafeGetValue(() => process.PagedMemorySize64, -1);
                info.PeakPagedMemorySize64 = SafeGetValue(() => process.PeakPagedMemorySize64, -1);
                info.WorkingSet64 = SafeGetValue(() => process.WorkingSet64, -1);
                info.PeakWorkingSet64 = SafeGetValue(() => process.PeakWorkingSet64, -1);
                info.VirtualMemorySize64 = SafeGetValue(() => process.VirtualMemorySize64, -1);
                info.PeakVirtualMemorySize64 = SafeGetValue(() => process.PeakVirtualMemorySize64, -1);
                info.PrivateMemorySize64 = SafeGetValue(() => process.PrivateMemorySize64, -1);
                info.PrivateWorkingSet64 = SafeGetValue(() => process.GetPrivateWorkingSet(), -1);

                info.MiniDump = new Uri(selfLink + "/dump");
                info.Parent = new Uri(selfLink, process.GetParentId().ToString());
                info.Children = process.GetChildren(recursive: false).Select(c => new Uri(selfLink, c.Id.ToString()));
            }

            return info;
        }

        private static Process GetProcessById(int id)
        {
            return id <= 0 ? Process.GetCurrentProcess() : Process.GetProcessById(id);
        }

        private T SafeGetValue<T>(Func<T> func, T defaultValue)
        {
            try
            {
                return func();
            }
            catch (Exception ex)
            {
                _tracer.TraceError(ex);
            }

            return defaultValue;
        }
    }
}