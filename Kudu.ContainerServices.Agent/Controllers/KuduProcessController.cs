using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Kudu.ContainerServices.Agent.Util;
using Kudu.Core.Diagnostics;

namespace Kudu.ContainerServices.Agent.Controllers
{
    [ApiController]
    [Route("/processes")]
    public class KuduProcessController : ControllerBase
    {
        [HttpGet]
        public IActionResult GetAllProcesses()
        {
            IEnumerable<ProcessInfo> processes = Process.GetProcesses()
                .Where(p => !AppServicePlatformProcess(p))  // Filter out platform processes
                .Select(p => GetProcessInfo(p, details: false, $"{p.Id}"));

            return Ok(processes);
        }

        [HttpGet("{id}")]
        public IActionResult GetProcess(int id)
        {
            Process p = GetProcessById(id);
            if (p == null)
            {
                return NotFound();
            }

            ProcessInfo info = GetProcessInfo(p, details: true, id.ToString());

            return Ok(info);
        }

        [HttpDelete, HttpPost]
        public IActionResult KillProcess(int id)
        {
            Process p = GetProcessById(id);
            if (p == null)
            {
                return NotFound();
            }
            p.Kill(entireProcessTree: true);

            return Ok();
        }

        [HttpGet("{id}/threads")]
        public IActionResult GetAllThreads(int id)
        {
            Process p = GetProcessById(id);
            if (p == null)
            {
                return NotFound();
            }
            IEnumerable<ProcessThreadInfo> threadsInfo = GetThreads(p);

            return Ok(threadsInfo);
        }

        [HttpGet("{processId}/threads/{threadId}")]
        public IActionResult GetThread(int processId, int threadId)
        {
            Process p = GetProcessById(processId);
            if (p == null)
            {
                return NotFound();
            }
            IEnumerable<ProcessThread> threads = p.Threads.Cast<ProcessThread>();
            ProcessThread thread = threads.FirstOrDefault(t => t.Id == threadId);
            if (thread == null)
            {
                return NotFound();
            }

            ProcessThreadInfo threadInfo = GetProcessThreadInfo(thread, processId, details: true);

            return Ok(threadInfo);
        }

        [HttpGet("{id}/modules")]
        public IActionResult GetAllModules(int id)
        {
            Process p = GetProcessById(id);
            if (p == null)
            {
                return NotFound();
            }
            IEnumerable<ProcessModuleInfo> modulesInfo = GetModules(p);

            return Ok(modulesInfo);
        }

        [HttpGet("{id}/modules/{baseAddress}")]
        public IActionResult GetModule(int id, string baseAddress)
        {
            Process p = GetProcessById(id);
            if (p == null)
            {
                return NotFound();
            }
            IEnumerable<ProcessModule> modules = p.Modules.Cast<ProcessModule>();
            ProcessModule module = modules.FirstOrDefault(t => t.BaseAddress.ToInt64() == Int64.Parse(baseAddress, NumberStyles.HexNumber));
            if (module == null)
            {
                return NotFound();
            }

            ProcessModuleInfo moduleInfo = GetProcessModuleInfo(module, details: true);

            return Ok(moduleInfo);
        }

        [HttpGet("{id}/environments/{filter}")]
        public IActionResult GetEnvironments(int id, string filter)
        {
            throw new NotImplementedException();
        }

        [HttpGet("{id}/dump")]
        public IActionResult MiniDump(int id, int dumpType = 0, string format = null)
        {
            throw new NotImplementedException();
        }

        [HttpPost("{id}/profile/start")]
        public IActionResult StartProfileAsync(int id, bool iisProfiling = false)
        {
            throw new NotImplementedException();
        }

        [HttpGet("{id}/profile/stop")]
        public IActionResult StopProfileAsync(int id)
        {
            throw new NotImplementedException();
        }



        private static Process GetProcessById(int processId)
        {
            if (processId == 0)
            {
                return Process.GetCurrentProcess();
            }
            else if (processId == -1)
            {
                // Order is important
                string[] processesOfInterest = new string[]
                {
                    "w3wp",
                    "dotnet",
                    "java",
                    "node"
                };

                foreach (string processOfInterest in processesOfInterest)
                {
                    Process p = Process.GetProcessesByName(processOfInterest).FirstOrDefault();
                    if (p != null && !AppServicePlatformProcess(p))
                    {
                        return p;
                    }
                }

                return null;
            }

            return Process.GetProcessById(processId);
        }

        private static ProcessInfo GetProcessInfo(Process process, bool details = false, string path = "")
        {
            var href = $"https://{Environment.GetEnvironmentVariable("HTTP_HOST")}/api/processes/{path}";
            if (href.EndsWith("/0", StringComparison.OrdinalIgnoreCase))
            {
                href = href.Substring(0, href.Length - 1) + process.Id;
            }
            var selfLink = new Uri(href);
            ProcessInfo info = new ProcessInfo()
            {
                Id = process.Id,
                Name = process.ProcessName,
                Href = selfLink,
                MachineName = Environment.MachineName,
                UserName = SafeGetValue(process.GetUserName, null)
            };

            if (details)
            {
                info.HandleCount = SafeGetValue(() => process.HandleCount, -1);
                info.ThreadCount = SafeGetValue(() => process.Threads.Count, -1);
                info.ModuleCount = SafeGetValue(() => process.Modules.Count, -1);
                info.FileName = SafeGetValue(() => process.MainModule.FileName, "N/A");
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
                info.Parent = new Uri(selfLink, SafeGetValue(() => process.GetParentId(), 0).ToString());
                info.Children = SafeGetValue(() => process.GetChildren(recursive: false), Enumerable.Empty<Process>()).Select(c => new Uri(selfLink, c.Id.ToString()));
                info.Threads = SafeGetValue(() => GetThreads(process), Enumerable.Empty<ProcessThreadInfo>());
                info.Modules = SafeGetValue(() => GetModules(process), Enumerable.Empty<ProcessModuleInfo>());
                info.TimeStamp = DateTime.UtcNow;
                info.EnvironmentVariables = SafeGetValue(process.GetEnvironmentVariables, new Dictionary<string, string>());
                info.CommandLine = SafeGetValue(process.GetCommandLine, null);
                // Not supported for Kudu Agent yet!
                info.OpenFileHandles = Enumerable.Empty<string>(); // SafeGetValue(() => GetOpenFileHandles(process.Id), Enumerable.Empty<string>());
                info.IsProfileRunning = false;          // ProfileManager.IsProfileRunning(process.Id);
                info.IsIisProfileRunning = false;       // ProfileManager.IsIisProfileRunning(process.Id);
                info.IisProfileTimeoutInSeconds = 0;    // ProfileManager.IisProfileTimeoutInSeconds;
                SetProcessEnvironmentInfo(info);
            }

            return info;
        }

        private static void SetProcessEnvironmentInfo(ProcessInfo processInfo)
        {
            if (processInfo.EnvironmentVariables != null)
            {
                processInfo.IsScmSite = false;
                processInfo.IsWebJob = SafeGetValue(() => ProcessExtensions.GetIsWebJob(processInfo.EnvironmentVariables), false);
                processInfo.Description = SafeGetValue(() => ProcessExtensions.GetDescription(processInfo.EnvironmentVariables), null);
            }
        }

        private static IEnumerable<ProcessThreadInfo> GetThreads(Process process)
        {
            IEnumerable<ProcessThread> threads = process.Threads.Cast<ProcessThread>();
            IEnumerable<ProcessThreadInfo> threadsInfo = threads.Select(t => GetProcessThreadInfo(t, process.Id, details: false));

            return threadsInfo;
        }

        private static IEnumerable<ProcessModuleInfo> GetModules(Process process)
        {
            IEnumerable<ProcessModule> modules = process.Modules.Cast<ProcessModule>();
            IEnumerable<ProcessModuleInfo> modulesInfo = modules.Select(m => GetProcessModuleInfo(m, details: false));

            return modulesInfo;
        }

        private static ProcessModuleInfo GetProcessModuleInfo(ProcessModule module, bool details = false)
        {
            ProcessModuleInfo moduleInfo = new ProcessModuleInfo()
            {
                BaseAddress = module.BaseAddress.ToInt64().ToString("x"),
                FileName = Path.GetFileName(module.FileName),
                FileVersion = module.FileVersionInfo.FileVersion
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

        private static ProcessThreadInfo GetProcessThreadInfo(ProcessThread thread, int pid, bool details = false)
        {
            var href = $"https://{Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME")}/api/processes/{pid}/threads/{thread.Id}";
            ProcessThreadInfo threadInfo = new ProcessThreadInfo()
            {
                Id = thread.Id,
                State = thread.ThreadState.ToString(),
                Href = new Uri(href)
            };

            if (details)
            {
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

        private static bool AppServicePlatformProcess(Process process)
        {
            // App Service processes inside the container are always launched from "appservice" folder.
            return SafeGetValue(() => process.MainModule.FileName.Contains("appservice"), false);
        }

        private static T SafeGetValue<T>(Func<T> func, T defaultValue)
        {
            try
            {
                return func();
            }
            catch
            {
                return defaultValue;
            }
        }
    }

    class ProcessNotFoundException : Exception
    {
        public ProcessNotFoundException() : base("Unable to find a process that matches the provided identifier") { }
    }
}
