using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Kudu.Contracts.Tracing;
using Kudu.Core.Deployment;
using Kudu.Core.Helpers;
using Kudu.Core.Tracing;
using Microsoft.Win32.SafeHandles;

namespace Kudu.Core.Infrastructure
{
    // http://blogs.msdn.com/b/bclteam/archive/2006/06/20/640259.aspx
    // http://stackoverflow.com/questions/394816/how-to-get-parent-process-in-net-in-managed-way
    public static class ProcessExtensions
    {
        internal static TimeSpan StandardOutputDrainTimeout = TimeSpan.FromSeconds(5);

        private readonly static Lazy<string> _clrRuntimeDirectory = new Lazy<string>(() =>
        {
            return RuntimeEnvironment.GetRuntimeDirectory();
        });

        public static string ClrRuntimeDirectory
        {
            get { return _clrRuntimeDirectory.Value; }
        }

        public static void Kill(this Process process, bool includesChildren, ITracer tracer)
        {
            try
            {
                if (includesChildren)
                {
                    foreach (Process child in process.GetChildren(tracer))
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

        public static IEnumerable<Process> GetChildren(this Process process, ITracer tracer, bool recursive = true)
        {
            int pid = process.Id;
            Dictionary<int, List<int>> tree = GetProcessTree(tracer);
            return GetChildren(pid, tree, recursive).Select(cid => SafeGetProcessById(cid)).Where(p => p != null);
        }

        // recursively get children.
        // return depth-first (leaf child first).
        private static IEnumerable<int> GetChildren(int pid, Dictionary<int, List<int>> tree, bool recursive)
        {
            List<int> children;
            if (tree.TryGetValue(pid, out children))
            {
                List<int> result = new List<int>();
                foreach (int id in children)
                {
                    if (recursive)
                    {
                        result.AddRange(GetChildren(id, tree, recursive));
                    }
                    result.Add(id);
                }
                return result;
            }
            return Enumerable.Empty<int>();
        }

        /// <summary>
        /// Calculates the sum of TotalProcessorTime for the current process and all its children.
        /// </summary>
        public static TimeSpan GetTotalProcessorTime(this Process process, ITracer tracer)
        {
            try
            {
                var processes = process.GetChildren(tracer).Concat(new[] { process }).Select(p => new { Name = p.ProcessName, Id = p.Id, Cpu = p.TotalProcessorTime });
                var totalTime = TimeSpan.FromTicks(processes.Sum(p => p.Cpu.Ticks));
                var info = String.Join("+", processes.Select(p => String.Format("{0}({1},{2:0.000}s)", p.Name, p.Id, p.Cpu.TotalSeconds)).ToArray());
                tracer.Trace("Cpu: {0}=total({1:0.000}s)", info, totalTime.TotalSeconds);
                return totalTime;
            }
            catch (Exception ex)
            {
                tracer.TraceError(ex);
            }

            return process.TotalProcessorTime;
        }

        /// <summary>
        /// Get parent process.
        /// </summary>
        public static Process GetParentProcess(this Process process, ITracer tracer)
        {
            if (!OSDetector.IsOnWindows())
            {
                return process.GetParentProcessLinux(tracer);
            }

            IntPtr processHandle;
            if (!process.TryGetProcessHandle(out processHandle))
            {
                return null;
            }

            var pbi = new ProcessNativeMethods.ProcessInformation();
            try
            {
                int returnLength;
                int status = ProcessNativeMethods.NtQueryInformationProcess(processHandle, 0, ref pbi, Marshal.SizeOf(pbi), out returnLength);
                if (status != 0)
                {
                    throw new Win32Exception(status);
                }

                return Process.GetProcessById(pbi.InheritedFromUniqueProcessId.ToInt32());
            }
            catch (Exception ex)
            {
                var processName = process.SafeGetProcessName() ?? "(null)";
                if (!processName.Equals("w3wp", StringComparison.OrdinalIgnoreCase))
                {
                    tracer.TraceError(ex, "GetParentProcess of {0}({1}) failed.", processName, process.Id);
                }
                return null;
            }
        }

        // http://stackoverflow.com/questions/2509406/c-mono-get-list-of-child-processes-on-windows-and-linux
        public static Process GetParentProcessLinux(this Process process, ITracer tracer)
        {
            try
            {
                var procPath = "/proc/" + process.Id + "/stat";

                var lines = File.ReadLines("/proc/" + process.Id + "/stat");
                var match = Regex.Match(lines.First(), @"\d+\s+\((.*?)\)\s+\w+\s+(\d+)\s");

                if (match.Success)
                {
                    var ppid = Int32.Parse(match.Groups[2].Value);
                    return ppid < 1 ? null : Process.GetProcessById(ppid);
                }
                tracer.TraceError("GetParentProcessLinux: Invalid proc stat format: " + procPath);
            }
            catch(FileNotFoundException)
            {
                tracer.TraceError("Could not find process with PID=" + process.Id);
            }
            catch(Exception ex)
            {
                tracer.TraceError(ex, "GetParentProcessLinux ({0}) failed.", process.Id);
            }
            return null;
        }

        /// <summary>
        /// Get parent id.
        /// </summary>
        public static int GetParentId(this Process process, ITracer tracer)
        {
            Process parent = process.GetParentProcess(tracer);
            return parent != null ? parent.Id : -1;
        }

        public static void MiniDump(this Process process, string dumpFile, MINIDUMP_TYPE dumpType)
        {
            using (var fs = new FileStream(dumpFile, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                if (!MiniDumpNativeMethods.MiniDumpWriteDump(process.Handle, (uint)process.Id, fs.SafeFileHandle, dumpType, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
        }

        public static async Task<int> Start(this IProcess process, ITracer tracer, Stream output, Stream error, Stream input = null, IdleManager idleManager = null)
        {
            var cancellationTokenSource = new CancellationTokenSource();

            process.Start();

            var tasks = new List<Task>();

            if (input != null)
            {
                tasks.Add(CopyStreamAsync(input, process.StandardInput.BaseStream, idleManager, cancellationTokenSource.Token, closeAfterCopy: true));
            }

            tasks.Add(CopyStreamAsync(process.StandardOutput.BaseStream, output, idleManager, cancellationTokenSource.Token));
            tasks.Add(CopyStreamAsync(process.StandardError.BaseStream, error, idleManager, cancellationTokenSource.Token));

            idleManager.WaitForExit(process);

            // Process has exited, draining the stdout and stderr
            await FlushAllAsync(process, tracer, idleManager, cancellationTokenSource, tasks);

            return process.ExitCode;
        }

        // Source: http://eazfuscator.blogspot.com/2011/06/reading-environment-variables-from.html
        public static Dictionary<string, string> GetEnvironmentVariables(this Process process)
        {
            IntPtr processHandle;
            if (!process.TryGetProcessHandle(out processHandle))
            {
                return null;
            }

            return GetEnvironmentVariablesCore(processHandle);
        }

        public static bool TryGetEnvironmentVariables(this Process process, out Dictionary<string, string> environmentVariables)
        {
            try
            {
                environmentVariables = GetEnvironmentVariables(process);
            }
            catch
            {
                environmentVariables = null;
            }

            return environmentVariables != null;
        }

        public static string GetUserName(this Process process)
        {
            IntPtr processHandle;
            if (!process.TryGetProcessHandle(out processHandle))
            {
                return null;
            }

            IntPtr processToken;
            if (!ProcessNativeMethods.OpenProcessToken(processHandle, ProcessNativeMethods.TOKEN_QUERY, out processToken))
            {
                throw new Win32Exception();
            }

            // ensure we call CloseHandle on processToken
            using (new SafeFileHandle(processToken, ownsHandle: true))
            {
                using (var identity = new WindowsIdentity(processToken))
                {
                    return identity.Name;
                }
            }
        }

        public static string GetCommandLine(this Process process)
        {
            IntPtr processHandle;
            if (!process.TryGetProcessHandle(out processHandle))
            {
                return null;
            }

            return GetCommandLineCore(processHandle);
        }

        public static bool GetIsScmSite(Dictionary<string, string> environment)
        {
            string appPool = null;
            if (environment.TryGetValue(WellKnownEnvironmentVariables.ApplicationPoolId, out appPool) &&
                !string.IsNullOrEmpty(appPool))
            {
                return appPool.StartsWith("~1", StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        public static bool GetIsWebJob(Dictionary<string, string> environment)
        {
            return environment.ContainsKey(WellKnownEnvironmentVariables.WebJobsName);
        }

        public static string GetDescription(Dictionary<string, string> environment)
        {
            const string webJobTemplate = "WebJob: {0}, Type: {1}";

            string webJobName = null;
            string webJobType = null;
            if (environment.TryGetValue(WellKnownEnvironmentVariables.WebJobsName, out webJobName) &&
                environment.TryGetValue(WellKnownEnvironmentVariables.WebJobsType, out webJobType))
            {
                return String.Format(webJobTemplate, webJobName, webJobType);
            }

            return null;
        }

        private static bool TryGetProcessHandle(this Process process, out IntPtr processHandle)
        {
            try
            {
                // for public kudu, this may fail due to access denied.
                // handle the exception to reduce noises in trace errors.
                processHandle = process.Handle;
            }
            catch (Win32Exception ex)
            {
                if (ex.NativeErrorCode != 5)
                {
                    throw;
                }

                processHandle = IntPtr.Zero;
            }

            return processHandle != IntPtr.Zero;
        }

        private static string SafeGetProcessName(this Process process)
        {
            try
            {
                return process.ProcessName;
            }
            catch(InvalidOperationException)
            {
                // The process has already exited
                return null;
            }
        }

        private static async Task CopyStreamAsync(Stream from, Stream to, IdleManager idleManager, CancellationToken cancellationToken, bool closeAfterCopy = false)
        {
            try
            {
                byte[] bytes = new byte[1024];
                int read = 0;
                while ((read = await from.ReadAsync(bytes, 0, bytes.Length, cancellationToken)) != 0)
                {
                    idleManager.UpdateActivity();
                    await to.WriteAsync(bytes, 0, read, cancellationToken);
                }

                idleManager.UpdateActivity();
            }
            finally
            {
                // this is needed specifically for input stream
                // in order to tell executable that the input is done
                if (closeAfterCopy)
                {
                    to.Close();
                }
            }
        }

        private static async Task FlushAllAsync(IProcess process, ITracer tracer, IdleManager idleManager, CancellationTokenSource cancellationTokenSource, IEnumerable<Task> tasks)
        {
            var prevActivity = DateTime.MinValue;
            while (true)
            {
                // Wait for either delay or io tasks
                var delay = Task.Delay(StandardOutputDrainTimeout, cancellationTokenSource.Token);
                var stdio = Task.WhenAll(tasks);
                var completed = await Task.WhenAny(stdio, delay);

                // if delay commpleted first (meaning timeout), check if activity and continue to wait
                if (completed == delay)
                {
                    var lastActivity = idleManager.LastActivity;
                    if (lastActivity != prevActivity)
                    {
                        prevActivity = lastActivity;
                        continue;
                    }
                }

                // clean up all pending tasks by cancelling them
                // this is important so we don't have runaway tasks
                cancellationTokenSource.Cancel();

                // in case of stdoutput/err have no activity within given time
                // we force close all streams
                if (completed == delay)
                {
                    using (tracer.Step("Flush stdio and stderr have no activity within given time"))
                    {
                        bool exited = process.HasExited;

                        SafeCloseStream(process.StandardOutput.BaseStream);
                        SafeCloseStream(process.StandardError.BaseStream);

                        // this means no activity within given time
                        // and process has not exited
                        if (!exited)
                        {
                            throw new TimeoutException("Timeout draining standard input, output and error!");
                        }
                    }
                }

                // happy path
                break;
            }
        }

        private static void SafeCloseStream(Stream stream)
        {
            try
            {
                stream.Close();
            }
            catch (Exception)
            {
                // no-op
            }
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
                int pid = process.Id;
                process.Kill();
                tracer.Trace("Abort Process '{0}({1})'.", processName, pid);
            }
            catch (Exception)
            {
                if (!process.HasExited)
                {
                    throw;
                }
            }
        }

        private static Dictionary<int, List<int>> GetProcessTree(ITracer tracer)
        {
            var tree = new Dictionary<int, List<int>>();
            foreach (var proc in Process.GetProcesses())
            {
                Process parent = proc.GetParentProcess(tracer);
                if (parent != null)
                {
                    List<int> children = null;
                    if (!tree.TryGetValue(parent.Id, out children))
                    {
                        tree[parent.Id] = children = new List<int>();
                    }

                    children.Add(proc.Id);
                }
            }

            return tree;
        }

        private static Dictionary<string, string> GetEnvironmentVariablesCore(IntPtr hProcess)
        {
            IntPtr penv = GetPenv(hProcess);

            int dataSize;
            if (!HasReadAccess(hProcess, penv, out dataSize))
            {
                throw new Win32Exception("Unable to read environment block.");
            }

            // Limit env size to 10 MB to be defensive
            const int maxEnvSize = 10 * 1000 * 1000;
            if (dataSize > maxEnvSize)
            {
                dataSize = maxEnvSize;
            }

            var envData = new byte[dataSize];
            var res_len = IntPtr.Zero;
            bool b = ProcessNativeMethods.ReadProcessMemory(
                hProcess,
                penv,
                envData,
                new IntPtr(dataSize),
                ref res_len);

            if (!b || (int)res_len != dataSize)
            {
                throw new Win32Exception("Unable to read environment block data.");
            }

            return EnvToDictionary(envData);
        }

        private static string GetCommandLineCore(IntPtr hProcess)
        {
            int targetProcessBitness = GetProcessBitness(hProcess);
            
            if (targetProcessBitness == 64 && !System.Environment.Is64BitProcess)
            {
                throw new Win32Exception(
                    "The current process should run in 64 bit mode to be able to get the environment of another 64 bit process.");
            }

            var pPeb = targetProcessBitness == 64 ? GetPeb64(hProcess) : GetPeb32(hProcess);
            //dt -r ntdll!PEB for offset values of the PEB
            var offset = targetProcessBitness == 64 ? 0x20 : 0x10;
            var unicodeStringOffset = targetProcessBitness == 64 ? 0x70 : 0x40;

            IntPtr ptr;
            if (!TryReadIntPtr(hProcess, pPeb + offset, out ptr))
            {
                throw new Win32Exception("Unable to read PEB.");
            }

            int commandLineLength;
            IntPtr commandLineBuffer;
            if ((targetProcessBitness == 64 && System.Environment.Is64BitProcess) ||
                (targetProcessBitness == 32 && !System.Environment.Is64BitProcess))
            {
                //we running same bitness as the target process, use native UNICODE_STRING
                var unicodeString = new ProcessNativeMethods.UNICODE_STRING();
                if (!ProcessNativeMethods.ReadProcessMemory(hProcess, ptr + unicodeStringOffset, ref unicodeString, new IntPtr(Marshal.SizeOf(unicodeString)), IntPtr.Zero))
                {
                    throw new Win32Exception(String.Format("Unable to read command line, win32 error {0}", Marshal.GetLastWin32Error()));
                }
                commandLineLength = unicodeString.Length;
                commandLineBuffer = unicodeString.Buffer;
            }
            else
            {
                //we are running 64 but target process is 32, use UNICODE_STRING_32
                var unicodeString = new ProcessNativeMethods.UNICODE_STRING_32();
                if (!ProcessNativeMethods.ReadProcessMemory(hProcess, ptr + unicodeStringOffset, ref unicodeString, new IntPtr(Marshal.SizeOf(unicodeString)), IntPtr.Zero))
                {
                    throw new Win32Exception(String.Format("Unable to read command line, win32 error {0}", Marshal.GetLastWin32Error()));
                }
                commandLineLength = unicodeString.Length;
                commandLineBuffer = new IntPtr(unicodeString.Buffer);
            }

            var bytes = new byte[commandLineLength];
            if (!ProcessNativeMethods.ReadProcessMemory(hProcess, commandLineBuffer, bytes, new IntPtr(commandLineLength), IntPtr.Zero))
            {
                throw new Win32Exception(String.Format("Unable to read command line, win32 error {0}", Marshal.GetLastWin32Error()));
            }

            return Encoding.Unicode.GetString(bytes);
        }

        private static Dictionary<string, string> EnvToDictionary(byte[] env)
        {
            var result = new Dictionary<string, string>();

            int len = env.Length;
            if (len < 4)
            {
                return result;
            }

            int n = len - 3;
            for (int i = 0; i < n; ++i)
            {
                byte c1 = env[i];
                byte c2 = env[i + 1];
                byte c3 = env[i + 2];
                byte c4 = env[i + 3];

                if (c1 == 0 && c2 == 0 && c3 == 0 && c4 == 0)
                {
                    len = i + 3;
                    break;
                }
            }

            // envs are key=value pair separated by '\0'
            var envs = Encoding.Unicode.GetString(env, 0, len).Split('\0');
            var separators = new[] { '=' };
            for (int i = 0; i < envs.Length; i++)
            {
                var pair = envs[i].Split(separators, 2);
                if (pair.Length != 2)
                {
                    continue;
                }
                result[pair[0]] = pair[1];
            }

            return result;
        }

        // should just be ReadIntPtr not TryReadIntPtr, throw when failed
        private static bool TryReadIntPtr(IntPtr hProcess, IntPtr ptr, out IntPtr readPtr)
        {
            var dataSize = new IntPtr(IntPtr.Size);
            var res_len = IntPtr.Zero;
            if (!ProcessNativeMethods.ReadProcessMemory(
                hProcess,
                ptr,
                out readPtr,
                dataSize,
                ref res_len))
            {
                // automatically GetLastError() and format message
                throw new Win32Exception();
            }

            // This is more like an assert
            return res_len == dataSize;
        }

        private static IntPtr GetPenv(IntPtr hProcess)
        {
            int processBitness = GetProcessBitness(hProcess);

            if (processBitness == 64)
            {
                if (!System.Environment.Is64BitProcess)
                {
                    throw new Win32Exception(
                        "The current process should run in 64 bit mode to be able to get the environment of another 64 bit process.");
                }

                IntPtr pPeb = GetPeb64(hProcess);

                IntPtr ptr;
                if (!TryReadIntPtr(hProcess, pPeb + 0x20, out ptr))
                {
                    throw new Win32Exception("Unable to read PEB.");
                }

                IntPtr penv;
                if (!TryReadIntPtr(hProcess, ptr + 0x80, out penv))
                {
                    throw new Win32Exception("Unable to read RTL_USER_PROCESS_PARAMETERS.");
                }

                return penv;
            }
            else
            {
                IntPtr pPeb = GetPeb32(hProcess);

                IntPtr ptr;
                if (!TryReadIntPtr(hProcess, pPeb + 0x10, out ptr))
                {
                    throw new Win32Exception("Unable to read PEB.");
                }

                IntPtr penv;
                if (!TryReadIntPtr(hProcess, ptr + 0x48, out penv))
                {
                    throw new Win32Exception("Unable to read RTL_USER_PROCESS_PARAMETERS.");
                }

                return penv;
            }
        }

        private static int GetProcessBitness(IntPtr hProcess)
        {
            if (System.Environment.Is64BitOperatingSystem)
            {
                bool wow64;
                if (!ProcessNativeMethods.IsWow64Process(hProcess, out wow64))
                {
                    return 32;
                }
                if (wow64)
                {
                    return 32;
                }

                return 64;
            }
            else
            {
                return 32;
            }
        }

        private static IntPtr GetPeb32(IntPtr hProcess)
        {
            if (System.Environment.Is64BitProcess)
            {
                var ptr = IntPtr.Zero;
                int res_len = 0;
                int pbiSize = IntPtr.Size;
                ProcessNativeMethods.NtQueryInformationProcess(
                    hProcess,
                    ProcessNativeMethods.ProcessWow64Information,
                    ref ptr,
                    pbiSize,
                    ref res_len);

                if (res_len != pbiSize)
                {
                    throw new Win32Exception("Unable to query process information.");
                }

                return ptr;
            }
            else
            {
                return GetPebNative(hProcess);
            }
        }

        private static IntPtr GetPebNative(IntPtr hProcess)
        {
            var pbi = new ProcessNativeMethods.ProcessInformation();
            int res_len = 0;
            int pbiSize = Marshal.SizeOf(pbi);
            ProcessNativeMethods.NtQueryInformationProcess(
                hProcess,
                ProcessNativeMethods.ProcessBasicInformation,
                ref pbi,
                pbiSize,
                out res_len);

            if (res_len != pbiSize)
            {
                throw new Win32Exception("Unable to query process information.");
            }

            return pbi.PebBaseAddress;
        }

        private static IntPtr GetPeb64(IntPtr hProcess)
        {
            return GetPebNative(hProcess);
        }

        private static bool HasReadAccess(IntPtr hProcess, IntPtr address, out int size)
        {
            size = 0;

            var memInfo = new ProcessNativeMethods.MEMORY_BASIC_INFORMATION();
            IntPtr result = ProcessNativeMethods.VirtualQueryEx(
                hProcess,
                address,
                ref memInfo,
                (IntPtr)Marshal.SizeOf(memInfo));

            if (result == IntPtr.Zero)
            {
                return false;
            }

            if (memInfo.Protect == ProcessNativeMethods.PAGE_NOACCESS || memInfo.Protect == ProcessNativeMethods.PAGE_EXECUTE)
            {
                return false;
            }

            try
            {
                size = Convert.ToInt32(memInfo.RegionSize.ToInt64() - (address.ToInt64() - memInfo.BaseAddress.ToInt64()));
            }
            catch (OverflowException)
            {
                return false;
            }

            if (size <= 0)
            {
                return false;
            }

            return true;
        }

        [SuppressUnmanagedCodeSecurity]
        internal static class ProcessNativeMethods
        {
            public const uint TOKEN_QUERY = 0x0008;

            [DllImport("ntdll.dll")]
            public static extern int NtQueryInformationProcess(
                IntPtr processHandle,
                int processInformationClass,
                ref ProcessInformation processInformation,
                int processInformationLength,
                out int returnLength);

            [DllImport("ntdll.dll", SetLastError = true)]
            public static extern int NtQueryInformationProcess(
                IntPtr processHandle,
                int processInformationClass,
                ref IntPtr processInformation,
                int processInformationLength,
                ref int returnLength);

            [StructLayout(LayoutKind.Sequential)]
            public struct ProcessInformation
            {
                // These members must match PROCESS_BASIC_INFORMATION
                internal IntPtr Reserved1;
                internal IntPtr PebBaseAddress;
                internal IntPtr Reserved2_0;
                internal IntPtr Reserved2_1;
                internal IntPtr UniqueProcessId;
                internal IntPtr InheritedFromUniqueProcessId;
            }

            public const int ProcessBasicInformation = 0;
            public const int ProcessWow64Information = 26;

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool ReadProcessMemory(
                IntPtr hProcess,
                IntPtr lpBaseAddress,
                [Out] byte[] lpBuffer,
                IntPtr dwSize,
                ref IntPtr lpNumberOfBytesRead);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool ReadProcessMemory(
                IntPtr hProcess,
                IntPtr lpBaseAddress,
                [Out] byte[] lpBuffer,
                IntPtr dwSize,
                IntPtr lpNumberOfBytesRead);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool ReadProcessMemory(
                IntPtr hProcess,
                IntPtr lpBaseAddress,
                out IntPtr lpPtr,
                IntPtr dwSize,
                ref IntPtr lpNumberOfBytesRead);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool ReadProcessMemory(
                IntPtr hProcess,
                IntPtr lpBaseAddress,
                ref UNICODE_STRING lpBuffer,
                IntPtr dwSize,
                IntPtr lpNumberOfBytesRead);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool ReadProcessMemory(
                IntPtr hProcess,
                IntPtr lpBaseAddress,
                ref UNICODE_STRING_32 lpBuffer,
                IntPtr dwSize,
                IntPtr lpNumberOfBytesRead);

            [DllImport("advapi32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool OpenProcessToken(
                IntPtr hProcess, 
                UInt32 dwDesiredAccess,
                out IntPtr processToken);

            [StructLayout(LayoutKind.Sequential)]
            public struct MEMORY_BASIC_INFORMATION
            {
                public IntPtr BaseAddress;
                public IntPtr AllocationBase;
                public int AllocationProtect;
                public IntPtr RegionSize;
                public int State;
                public int Protect;
                public int Type;
            }

            public const int PAGE_NOACCESS = 0x01;
            public const int PAGE_EXECUTE = 0x10;

            [DllImport("kernel32")]
            public static extern IntPtr VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, ref MEMORY_BASIC_INFORMATION lpBuffer, IntPtr dwLength);

            [DllImport("kernel32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool IsWow64Process(IntPtr hProcess, [MarshalAs(UnmanagedType.Bool)]out bool wow64Process);

            [StructLayout(LayoutKind.Sequential)]
            public struct UNICODE_STRING
            {
                public ushort Length;
                public ushort MaximumLength;
                public IntPtr Buffer;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct UNICODE_STRING_32
            {
                public ushort Length;
                public ushort MaximumLength;
                public int Buffer;
            }
        }
    }
}