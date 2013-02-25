using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Kudu.Contracts.Tracing;

namespace Kudu.Core.Infrastructure
{
    // http://blogs.msdn.com/b/bclteam/archive/2006/06/20/640259.aspx
    public static class ProcessExtensions
    {
        public static void Kill(this Process process, bool includesChildren, ITracer tracer)
        {
            try
            {
                if (includesChildren)
                {
                    foreach (Process child in process.GetChildren())
                    {
                        SafeKillProcess(child, tracer);
                    }
                }
            }
            catch (Exception ex)
            {
                tracer.TraceError(ex);
            }
            finally
            {
                SafeKillProcess(process, tracer);
            }
        }

        public static IEnumerable<Process> GetChildren(this Process process)
        {
            int pid = process.Id;
            Dictionary<int, List<int>> tree = GetProcessTree();
            List<int> children = new List<int>();
            if (tree.TryGetValue(pid, out children))
            {
                return children.Select(cid => SafeGetProcessById(cid)).Where(p => p != null);
            }

            return Enumerable.Empty<Process>();
        }

        private static Process SafeGetProcessById(int pid)
        {
            try
            {
                return Process.GetProcessById(pid);
            }
            catch (ArgumentException)
            {
                // Process with an Id is not running.
                return null;
            }
        }

        private static void SafeKillProcess(Process process, ITracer tracer)
        {
            try
            {
                string processName = process.ProcessName;
                process.Kill();
                tracer.Trace("Abort Process '{0}'.", processName);
            }
            catch (Exception)
            {
                if (!process.HasExited)
                {
                    throw;
                }
            }
        }

        private static Dictionary<int, List<int>> GetProcessTree()
        {
            var tree = new Dictionary<int, List<int>>();
            foreach (var proc in Process.GetProcesses().ToDictionary(p => p.Id, p => p.ProcessName))
            {
                string indexedProcessName = FindIndexedProcessName(proc.Key, proc.Value);
                if (String.IsNullOrEmpty(indexedProcessName))
                {
                    continue;
                }

                int parentId = FindPidFromIndexedProcessName(indexedProcessName);
                List<int> children = null;
                if (!tree.TryGetValue(parentId, out children))
                {
                    tree[parentId] = children = new List<int>();
                }

                children.Add(proc.Key);
            }

            return tree;
        }

        private static string FindIndexedProcessName(int pid, string processName)
        {
            string processIndexedName = null;
            Process[] processesByName = Process.GetProcessesByName(processName);
            for (var index = 0; index < processesByName.Length; index++)
            {
                processIndexedName = index == 0 ? processName : processName + "#" + index;
                using (var processId = new PerformanceCounter("Process", "ID Process", processIndexedName))
                {
                    if ((int)processId.NextValue() == pid)
                    {
                        return processIndexedName;
                    }
                }
            }

            return processIndexedName;
        }

        private static int FindPidFromIndexedProcessName(string indexedProcessName)
        {
            using (var parentId = new PerformanceCounter("Process", "Creating Process ID", indexedProcessName))
            {
                return (int)parentId.NextValue();
            }
        }
    }
}