using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web.Http;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Core.Diagnostics;
using Kudu.Core.Infrastructure;
using Kudu.Services.Infrastructure;

namespace Kudu.Services.Performance
{
    public class ProcessController : ApiController
    {
        private const string FreeSitePolicy = "Shared|Limited";

        private readonly ITracer _tracer;
        private readonly IEnvironment _environment;
        private readonly IFileSystem _fileSystem;
        private readonly IDeploymentSettingsManager _settings;

        public ProcessController(ITracer tracer,
                                 IEnvironment environment,
                                 IDeploymentSettingsManager settings,
                                 IFileSystem fileSystem)
        {
            _tracer = tracer;
            _environment = environment;
            _settings = settings;
            _fileSystem = fileSystem;
        }

        [HttpGet]
        public HttpResponseMessage GetThread(int processId, int threadId)
        {
            using (_tracer.Step("ProcessController.GetThread"))
            {
                var process = GetProcessById(processId);                

                foreach (ProcessThread thread in process.Threads)
                {
                    if(thread.Id == threadId)
                    {
                        return Request.CreateResponse(HttpStatusCode.OK, GetProcessThreadInfo(thread, Request.RequestUri.AbsoluteUri, true));
                    }
                }

                return Request.CreateResponse(HttpStatusCode.OK);
            }
        }


        [HttpGet]
        public HttpResponseMessage GetAllThreads(int id)
        {
            using (_tracer.Step("ProcessController.GetAllThreads"))
            {
                var process = GetProcessById(id);
                var results = new List<ProcessThreadInfo>();

                foreach (ProcessThread thread in process.Threads)
                {
                    results.Add(GetProcessThreadInfo(thread, Request.RequestUri.AbsoluteUri.TrimEnd('/') + '/' + thread.Id, false));
                }

                return Request.CreateResponse(HttpStatusCode.OK, results);
            }
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
                string sitePolicy = _settings.GetWebSitePolicy();
                if ((MINIDUMP_TYPE)dumpType == MINIDUMP_TYPE.WithFullMemory && sitePolicy.Equals(FreeSitePolicy, StringComparison.OrdinalIgnoreCase))
                {
                    return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, 
                        String.Format(CultureInfo.CurrentCulture, Resources.Error_FullMiniDumpNotSupported, sitePolicy));
                }

                var process = GetProcessById(id);

                string dumpFile = Path.Combine(_environment.LogFilesPath, "minidump", "minidump.dmp");
                FileSystemHelpers.EnsureDirectory(Path.GetDirectoryName(dumpFile));
                FileSystemHelpers.DeleteFileSafe(_fileSystem, dumpFile);

                try
                {
                    _tracer.Trace("MiniDump pid={0}, name={1}, file={2}", process.Id, process.ProcessName, dumpFile);
                    process.MiniDump(dumpFile, (MINIDUMP_TYPE)dumpType);
                    _tracer.Trace("MiniDump size={0}", new FileInfo(dumpFile).Length);
                }
                catch (Exception ex)
                {
                    FileSystemHelpers.DeleteFileSafe(_fileSystem, dumpFile);
                    return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.Message);
                }

                var context = new System.Web.HttpContextWrapper(System.Web.HttpContext.Current);
                string responseFileName = String.Format(CultureInfo.InvariantCulture, "{0}-{1}-{2:MM-dd-H:mm:ss}.dmp", process.ProcessName, InstanceIdUtility.GetShortInstanceId(context), DateTime.UtcNow);
                HttpResponseMessage response = Request.CreateResponse();
                response.Content = new StreamContent(MiniDumpStream.OpenRead(dumpFile, _fileSystem));
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment");
                response.Content.Headers.ContentDisposition.FileName = responseFileName;
                return response;
            }
        }

        private IEnumerable<ProcessThreadInfo> GetThreads(Process process, string href)
        {
            List<ProcessThreadInfo> threads = new List<ProcessThreadInfo>();
            foreach (ProcessThread thread in process.Threads)
            {
                threads.Add(GetProcessThreadInfo(thread, href + @"/threads/" + thread.Id, false));
            }

            return threads;
        }

        private ProcessThreadInfo GetProcessThreadInfo(ProcessThread thread, string href, bool details = false)
        {
            var threadInfo = new ProcessThreadInfo
            {
                Id = thread.Id,
                State = thread.ThreadState.ToString(),
                Href = new Uri(href)
            };
            
            if(details)
            {
                threadInfo.Process = new Uri(href.Substring(0, href.IndexOf(@"/threads/")));
                threadInfo.BasePriority = SafeGetValue(() => thread.BasePriority, -1);
                threadInfo.PriorityLevel = thread.PriorityLevel.ToString();
                threadInfo.CurrentPriority = SafeGetValue(() => thread.CurrentPriority, -1);
                threadInfo.StartTime = SafeGetValue(() => thread.StartTime.ToUniversalTime(), DateTime.MinValue);
                threadInfo.TotalProcessorTime = SafeGetValue(() => thread.TotalProcessorTime, TimeSpan.FromSeconds(-1));
                threadInfo.UserProcessorTime = SafeGetValue(() => thread.UserProcessorTime, TimeSpan.FromSeconds(-1));
                threadInfo.PriviledgedProcessorTime = SafeGetValue(() => thread.PrivilegedProcessorTime, TimeSpan.FromSeconds(-1));
                threadInfo.StartAddress = "0x" + thread.StartAddress.ToInt64().ToString("X");

                if (thread.ThreadState == ThreadState.Wait)
                {
                    threadInfo.WaitReason = thread.WaitReason.ToString();
                }
                else
                {
                    threadInfo.WaitReason = "Cannot obtain wait reason unless thread is in waiting state";
                }
            }

            return threadInfo;
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
                
                info.MiniDump = new Uri(selfLink + "/dump");
                info.Parent = new Uri(selfLink, SafeGetValue(() => process.GetParentId(_tracer), 0).ToString());
                info.Children = SafeGetValue(() => process.GetChildren(_tracer, recursive: false), Enumerable.Empty<Process>()).Select(c => new Uri(selfLink, c.Id.ToString()));
                info.Threads = SafeGetValue(() => GetThreads(process, selfLink.ToString()), Enumerable.Empty<ProcessThreadInfo>());
            }

            return info;
        }

        private Process GetProcessById(int id)
        {
            try
            {
                return id <= 0 ? Process.GetCurrentProcess() : Process.GetProcessById(id);
            }
            catch (ArgumentException ex)
            {
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.NotFound, ex.Message));
            }
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

        public class MiniDumpStream : DelegatingStream
        {
            private readonly string _path;
            private readonly IFileSystem _fileSystem;

            private MiniDumpStream(string path, IFileSystem fileSystem)
                : base(fileSystem.File.OpenRead(path))
            {
                _path = path;
                _fileSystem = fileSystem;
            }

            protected override void Dispose(bool disposing)
            {
                try
                {
                    base.Dispose(disposing);
                }
                finally
                {
                    FileSystemHelpers.DeleteFileSafe(_fileSystem, _path);
                }
            }

            public static Stream OpenRead(string path, IFileSystem fileSystem)
            {
                return new MiniDumpStream(path, fileSystem);
            }
        }
    }
}