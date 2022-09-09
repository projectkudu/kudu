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
using Microsoft.Win32.SafeHandles;

namespace Kudu.ContainerServices.Agent.Util
{
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

        public static IEnumerable<Process> GetChildren(this Process process, bool recursive = true)
        {
            int pid = process.Id;
            Dictionary<int, List<int>> tree = GetProcessTree();
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

        // Calculates the sum of TotalProcessorTime for the current process and all its children.
        public static TimeSpan GetTotalProcessorTime(this Process process)
        {
            try
            {
                var processes = process.GetChildren().Concat(new[] { process }).Select(p => new { Name = p.ProcessName, Id = p.Id, Cpu = p.TotalProcessorTime });
                var totalTime = TimeSpan.FromTicks(processes.Sum(p => p.Cpu.Ticks));
                var info = String.Join("+", processes.Select(p => String.Format("{0}({1},{2:0.000}s)", p.Name, p.Id, p.Cpu.TotalSeconds)).ToArray());

                return totalTime;
            }
            catch (Exception ex)
            {
                return process.TotalProcessorTime;
            }            
        }

        public static Process GetParentProcess(this Process process)
        {
            try
            {
                IntPtr processHandle;
                if (!process.TryGetProcessHandle(out processHandle))
                {
                    return null;
                }

                var pbi = new ProcessNativeMethods.ProcessInformation();
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
                return null;
            }
        }

        public static int GetParentId(this Process process)
        {
            Process parent = process.GetParentProcess();
            return parent != null ? parent.Id : -1;
        }        

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

        public static bool GetIsWebJob(Dictionary<string, string> environment)
        {
            return environment.ContainsKey("WEBJOBS_NAME");
        }

        public static string GetDescription(Dictionary<string, string> environment)
        {
            const string webJobTemplate = "WebJob: {0}, Type: {1}";

            string webJobName;
            string webJobType;
            if (environment.TryGetValue("WEBJOBS_NAME", out webJobName) &&
                environment.TryGetValue("WEBJOBS_TYPE", out webJobType))
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
                if (!process.HasExited && ex.NativeErrorCode != 5)
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
            catch (InvalidOperationException)
            {
                // The process has already exited
                return null;
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

        private static Dictionary<int, List<int>> GetProcessTree()
        {
            var tree = new Dictionary<int, List<int>>();
            foreach (var proc in Process.GetProcesses())
            {
                Process parent = proc.GetParentProcess();
                if (parent != null)
                {
                    if (!tree.TryGetValue(parent.Id, out var children))
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
                throw new Win32Exception("The current process should run in 64 bit mode to be able to get the environment of another 64 bit process.");
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
                // we running same bitness as the target process, use native UNICODE_STRING
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
                // we are running 64 but target process is 32, use UNICODE_STRING_32
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
            public static extern bool IsWow64Process(IntPtr hProcess, [MarshalAs(UnmanagedType.Bool)] out bool wow64Process);

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

            [DllImport("kernel32")]
            public static extern uint GetProcessId(IntPtr hProcess);

            [DllImport("kernel32", SetLastError = true)]
            public static extern IntPtr OpenProcess(uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint dwProcessId);

            [DllImport("kernel32", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

            [DllImport("kernel32", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool CloseHandle(IntPtr handle);

            public const uint PROCESS_TERMINATE = 0x0001;
        }
    }
}