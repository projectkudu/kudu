using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
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
using Kudu.Core.Tracing;
using Kudu.Services.Arm;
using Kudu.Services.Infrastructure;
using System.Threading.Tasks;
using System.Text;
using System.Threading;
using Kudu.Services.Diagnostics;

namespace Kudu.Services.Performance
{
    public class ProcessController : ApiController
    {

        private readonly ITracer _tracer;
        private readonly IEnvironment _environment;
        private readonly IDeploymentSettingsManager _settings;

        public ProcessController(ITracer tracer,
                                 IEnvironment environment,
                                 IDeploymentSettingsManager settings)
        {
            _tracer = tracer;
            _environment = environment;
            _settings = settings;
        }

        [HttpGet]
        public HttpResponseMessage GetThread(int processId, int threadId)
        {
            using (_tracer.Step("ProcessController.GetThread"))
            {
                var process = GetProcessById(processId);
                var thread = process.Threads.Cast<ProcessThread>().FirstOrDefault(t => t.Id == threadId);

                if (thread != null)
                {
                    return Request.CreateResponse(HttpStatusCode.OK, ArmUtils.AddEnvelopeOnArmRequest(GetProcessThreadInfo(thread, Request.RequestUri.AbsoluteUri, true), Request));
                }
                else
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound);
                }
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

                return Request.CreateResponse(HttpStatusCode.OK, ArmUtils.AddEnvelopeOnArmRequest(results, Request));
            }
        }

        [HttpGet]
        public HttpResponseMessage GetModule(int id, string baseAddress)
        {
            using (_tracer.Step("ProcessController.GetModule"))
            {

                var module = GetProcessById(id).Modules.Cast<ProcessModule>().FirstOrDefault(t => t.BaseAddress.ToInt64() == Int64.Parse(baseAddress, NumberStyles.HexNumber));

                if (module != null)
                {
                    var results = GetProcessModuleInfo(module, Request.RequestUri.AbsoluteUri, details: true);
                    return Request.CreateResponse(HttpStatusCode.OK, ArmUtils.AddEnvelopeOnArmRequest(results, Request));
                }
                else
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound);
                }
            }
        }

        [HttpGet]
        public HttpResponseMessage GetAllModules(int id)
        {
            using (_tracer.Step("ProcessController.GetAllModules"))
            {
                var results = GetModules(GetProcessById(id), Request.RequestUri.AbsoluteUri.TrimEnd('/'));
                return Request.CreateResponse(HttpStatusCode.OK, ArmUtils.AddEnvelopeOnArmRequest(results, Request));
            }
        }

        [HttpGet]
        public HttpResponseMessage GetAllProcesses(bool allUsers = false)
        {
            using (_tracer.Step("ProcessController.GetAllProcesses"))
            {
                var currentUser = Process.GetCurrentProcess().GetUserName();
                var results = Process.GetProcesses()
                    .Where(p => allUsers || String.Equals(currentUser, SafeGetValue(p.GetUserName, null), StringComparison.OrdinalIgnoreCase))
                    .Select(p => GetProcessInfo(p, Request.RequestUri.GetLeftPart(UriPartial.Path).TrimEnd('/') + '/' + p.Id)).OrderBy(p => p.Name.ToLowerInvariant())
                    .ToList();
                return Request.CreateResponse(HttpStatusCode.OK, ArmUtils.AddEnvelopeOnArmRequest(results, Request));
            }
        }

        [HttpGet]
        public HttpResponseMessage GetProcess(int id)
        {
            using (_tracer.Step("ProcessController.GetProcess"))
            {
                var process = GetProcessById(id);
                return Request.CreateResponse(HttpStatusCode.OK, ArmUtils.AddEnvelopeOnArmRequest(GetProcessInfo(process, Request.RequestUri.AbsoluteUri, details: true), Request));
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
        public HttpResponseMessage MiniDump(int id, int dumpType = 0, string format = null)
        {
            using (_tracer.Step("ProcessController.MiniDump"))
            {
                DumpFormat dumpFormat = ParseDumpFormat(format, DumpFormat.Raw);
                if (dumpFormat != DumpFormat.Raw && dumpFormat != DumpFormat.Zip)
                {
                    return Request.CreateErrorResponse(HttpStatusCode.BadRequest,
                        String.Format(CultureInfo.CurrentCulture, Resources.Error_DumpFormatNotSupported, dumpFormat));
                }

                string siteSku = _settings.GetWebSiteSku();
                if ((MINIDUMP_TYPE)dumpType == MINIDUMP_TYPE.WithFullMemory && siteSku.Equals(Constants.FreeSKU, StringComparison.OrdinalIgnoreCase))
                {
                    return Request.CreateErrorResponse(HttpStatusCode.InternalServerError,
                        String.Format(CultureInfo.CurrentCulture, Resources.Error_FullMiniDumpNotSupported, siteSku));
                }

                var process = GetProcessById(id);

                string dumpFile = Path.Combine(_environment.LogFilesPath, "minidump", "minidump.dmp");
                FileSystemHelpers.EnsureDirectory(Path.GetDirectoryName(dumpFile));
                FileSystemHelpers.DeleteFileSafe(dumpFile);

                try
                {
                    using (_tracer.Step(String.Format("MiniDump pid={0}, name={1}, file={2}", process.Id, process.ProcessName, dumpFile)))
                    {
                        process.MiniDump(dumpFile, (MINIDUMP_TYPE)dumpType);
                        _tracer.Trace("MiniDump size={0}", new FileInfo(dumpFile).Length);
                    }
                }
                catch (Exception ex)
                {
                    _tracer.TraceError(ex);
                    FileSystemHelpers.DeleteFileSafe(dumpFile);
                    return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.Message);
                }

                if (dumpFormat == DumpFormat.Raw)
                {
                    string responseFileName = GetResponseFileName(process.ProcessName, "dmp");

                    HttpResponseMessage response = Request.CreateResponse();
                    response.Content = new StreamContent(FileStreamWrapper.OpenRead(dumpFile));
                    response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                    response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment");
                    response.Content.Headers.ContentDisposition.FileName = responseFileName;
                    return response;
                }
                else if (dumpFormat == DumpFormat.Zip)
                {
                    string responseFileName = GetResponseFileName(process.ProcessName, "zip");

                    HttpResponseMessage response = Request.CreateResponse();
                    response.Content = ZipStreamContent.Create(responseFileName, _tracer, zip =>
                    {
                        try
                        {
                            zip.AddFile(dumpFile, _tracer, String.Empty);
                        }
                        finally
                        {
                            FileSystemHelpers.DeleteFileSafe(dumpFile);
                        }

                        foreach (var fileName in new[] { "sos.dll", "mscordacwks.dll" })
                        {
                            string filePath = Path.Combine(ProcessExtensions.ClrRuntimeDirectory, fileName);
                            if (FileSystemHelpers.FileExists(filePath))
                            {
                                zip.AddFile(filePath, _tracer, String.Empty);
                            }
                        }
                    });
                    return response;
                }
                else
                {
                    return Request.CreateErrorResponse(HttpStatusCode.BadRequest,
                        String.Format(CultureInfo.CurrentCulture, Resources.Error_DumpFormatNotSupported, dumpFormat));
                }
            }
        }

        [HttpPost]
        public async Task<HttpResponseMessage> StartProfileAsync(int id)
        {
            using (_tracer.Step("ProcessController.StartProfileAsync"))
            {
                // check if the process Ids exists in the sandbox. If it doesn't, this method returns a 404 and we are done.
                var process = GetProcessById(id);

                var result = await ProfileManager.StartProfileAsync(process.Id, _tracer);

                if(result.StatusCode != HttpStatusCode.OK)
                {
                    return Request.CreateErrorResponse(result.StatusCode, result.Message);
                }
                else
                {
                    return Request.CreateResponse(HttpStatusCode.OK);
                }
            }
        }

        [HttpGet]
        public async Task<HttpResponseMessage> StopProfileAsync(int id)
        {
            using (_tracer.Step("ProcessController.StopProfileAsync"))
            {
                // check if the process Ids exists in the sandbox. If it doesn't, this method returns a 404 and we are done.
                var process = GetProcessById(id);

                var result = await ProfileManager.StopProfileAsync(process.Id, _tracer);

                if (result.StatusCode != HttpStatusCode.OK)
                {
                    return Request.CreateErrorResponse(result.StatusCode, result.Message);
                }
                else
                {
                    string profileFileFullPath = ProfileManager.GetProfilePath(process.Id);
                    string profileFileName = Path.GetFileName(profileFileFullPath);

                    HttpResponseMessage response = Request.CreateResponse();
                    response.Content = new StreamContent(FileStreamWrapper.OpenRead(profileFileFullPath));
                    response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                    response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment");
                    response.Content.Headers.ContentDisposition.FileName = profileFileName;
                    return response;
                }
            }
        }

        private static string GetResponseFileName(string prefix, string ext)
        {
            return String.Format(CultureInfo.InvariantCulture, "{0}-{1}-{2:MM-dd-HH-mm-ss}.{3}", prefix, InstanceIdUtility.GetShortInstanceId(), DateTime.UtcNow, ext);
        }

        private DumpFormat ParseDumpFormat(string format, DumpFormat defaultFormat)
        {
            if (String.IsNullOrEmpty(format))
            {
                return defaultFormat;
            }

            try
            {
                return (DumpFormat)Enum.Parse(typeof(DumpFormat), format, ignoreCase: true);
            }
            catch (Exception ex)
            {
                _tracer.TraceError(ex);

                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, ex.Message));
            }
        }

        private IEnumerable<string> GetOpenFileHandles(int processId)
        {
            var exe = new Executable(Path.Combine(_environment.ScriptPath, "KuduHandles.exe"), _environment.RootPath,
                _settings.GetCommandIdleTimeout());
            var result = exe.Execute(_tracer, processId.ToString());
            var stdout = result.Item1;
            var stderr = result.Item2;

            if (!String.IsNullOrEmpty(stderr))
                _tracer.TraceError(stderr);

            if (String.IsNullOrEmpty(stdout))
                return Enumerable.Empty<string>();
            return stdout.Split(new[] { System.Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        private IEnumerable<ProcessThreadInfo> GetThreads(Process process, string href)
        {
            List<ProcessThreadInfo> threads = new List<ProcessThreadInfo>();
            foreach (ProcessThread thread in process.Threads)
            {
                threads.Add(GetProcessThreadInfo(thread, href + @"/threads/" + thread.Id));
            }

            return threads;
        }

        private static IEnumerable<ProcessModuleInfo> GetModules(Process process, string href)
        {
            var modules = new List<ProcessModuleInfo>();
            foreach (var module in process.Modules.Cast<ProcessModule>().OrderBy(m => Path.GetFileName(m.FileName)))
            {
                modules.Add(GetProcessModuleInfo(module, href.TrimEnd('/') + '/' + module.BaseAddress.ToInt64().ToString("x"), details: false));
            }

            return modules;
        }

        private ProcessThreadInfo GetProcessThreadInfo(ProcessThread thread, string href, bool details = false)
        {
            var threadInfo = new ProcessThreadInfo
            {
                Id = thread.Id,
                State = thread.ThreadState.ToString(),
                Href = new Uri(href)
            };

            if (details)
            {
                threadInfo.Process = new Uri(href.Substring(0, href.IndexOf(@"/threads/", StringComparison.OrdinalIgnoreCase)));
                threadInfo.BasePriority = SafeGetValue(() => thread.BasePriority, -1);
                threadInfo.PriorityLevel = thread.PriorityLevel.ToString();
                threadInfo.CurrentPriority = SafeGetValue(() => thread.CurrentPriority, -1);
                threadInfo.StartTime = SafeGetValue(() => thread.StartTime.ToUniversalTime(), DateTime.MinValue);
                threadInfo.TotalProcessorTime = SafeGetValue(() => thread.TotalProcessorTime, TimeSpan.FromSeconds(-1));
                threadInfo.UserProcessorTime = SafeGetValue(() => thread.UserProcessorTime, TimeSpan.FromSeconds(-1));
                threadInfo.PriviledgedProcessorTime = SafeGetValue(() => thread.PrivilegedProcessorTime, TimeSpan.FromSeconds(-1));
                threadInfo.StartAddress = "0x" + thread.StartAddress.ToInt64().ToString("X");

                if (thread.ThreadState == System.Diagnostics.ThreadState.Wait)
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

        private static ProcessModuleInfo GetProcessModuleInfo(ProcessModule module, string href, bool details = false)
        {
            var moduleInfo = new ProcessModuleInfo
            {
                BaseAddress = module.BaseAddress.ToInt64().ToString("x"),
                FileName = Path.GetFileName(module.FileName),
                FileVersion = module.FileVersionInfo.FileVersion,
                Href = new Uri(href)
            };

            if (details)
            {
                moduleInfo.FilePath = module.FileName;
                moduleInfo.ModuleMemorySize = module.ModuleMemorySize;
                moduleInfo.FileDescription = module.FileVersionInfo.FileDescription;
                moduleInfo.Product = module.FileVersionInfo.ProductName;
                moduleInfo.ProductVersion = module.FileVersionInfo.ProductVersion;
                moduleInfo.IsDebug = module.FileVersionInfo.IsDebug;
                moduleInfo.Language = module.FileVersionInfo.Language;
            }

            return moduleInfo;
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
                Href = selfLink,
                UserName = SafeGetValue(process.GetUserName, null)
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
                info.OpenFileHandles = SafeGetValue(() => GetOpenFileHandles(process.Id), Enumerable.Empty<string>());
                info.Parent = new Uri(selfLink, SafeGetValue(() => process.GetParentId(_tracer), 0).ToString());
                info.Children = SafeGetValue(() => process.GetChildren(_tracer, recursive: false), Enumerable.Empty<Process>()).Select(c => new Uri(selfLink, c.Id.ToString()));
                info.Threads = SafeGetValue(() => GetThreads(process, selfLink.ToString()), Enumerable.Empty<ProcessThreadInfo>());
                info.Modules = SafeGetValue(() => GetModules(process, selfLink.ToString().TrimEnd('/') + "/modules"), Enumerable.Empty<ProcessModuleInfo>());
                info.TimeStamp = DateTime.UtcNow;
                info.EnvironmentVariables = SafeGetValue(process.GetEnvironmentVariables, null);
                info.CommandLine = SafeGetValue(process.GetCommandLine, null);
                info.IsProfileRunning = ProfileManager.IsProfileRunnig(process.Id);
                SetEnvironmentInfo(info);
            }

            return info;
        }

        internal void SetEnvironmentInfo(ProcessInfo processInfo)
        {
            if (processInfo.EnvironmentVariables != null)
            {
                processInfo.IsScmSite = SafeGetValue(() => ProcessExtensions.GetIsScmSite(processInfo.EnvironmentVariables), false);
                processInfo.IsWebJob = SafeGetValue(() => ProcessExtensions.GetIsWebJob(processInfo.EnvironmentVariables), false);
                processInfo.Description = SafeGetValue(() => ProcessExtensions.GetDescription(processInfo.EnvironmentVariables), null);
            }
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
                // skip the known access denied to reduce noise in trace
                var win32Exception = ex as Win32Exception;
                if (win32Exception == null || win32Exception.NativeErrorCode != 5)
                {
                    _tracer.TraceError(ex);
                }
            }

            return defaultValue;
        }

        public enum DumpFormat
        {
            Raw,
            Zip,
        }

        public class FileStreamWrapper : DelegatingStream
        {
            private readonly string _path;

            private FileStreamWrapper(string path)
                : base(FileSystemHelpers.OpenRead(path))
            {
                _path = path;
            }

            protected override void Dispose(bool disposing)
            {
                try
                {
                    base.Dispose(disposing);
                }
                finally
                {
                    FileSystemHelpers.DeleteFileSafe(_path);
                }
            }

            public static Stream OpenRead(string path)
            {
                return new FileStreamWrapper(path);
            }
        }        
    }
}