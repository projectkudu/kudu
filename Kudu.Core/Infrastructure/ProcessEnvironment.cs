using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Kudu.Core.Infrastructure
{
    // Source: http://eazfuscator.blogspot.com/2011/06/reading-environment-variables-from.html
    public static class ProcessEnvironment
    {
        public static StringDictionary GetEnvironmentVariables(Process process)
        {
            return GetEnvironmentVariablesCore(process.Handle);
        }

        public static StringDictionary TryGetEnvironmentVariables(Process process)
        {
            try
            {
                return GetEnvironmentVariables(process);
            }
            catch
            {
                return null;
            }
        }

        private static StringDictionary GetEnvironmentVariablesCore(IntPtr hProcess)
        {
            IntPtr penv = GetPenv(hProcess);

            int dataSize;
            if (!HasReadAccess(hProcess, penv, out dataSize))
            {
                throw new InvalidOperationException("Unable to read environment block.");
            }

            const int maxEnvSize = 32767;
            if (dataSize > maxEnvSize)
            {
                dataSize = maxEnvSize;
            }

            var envData = new byte[dataSize];
            var res_len = IntPtr.Zero;
            bool b = NativeMethods.ReadProcessMemory(
                hProcess,
                penv,
                envData,
                new IntPtr(dataSize),
                ref res_len);

            if (!b || (int)res_len != dataSize)
            {
                throw new InvalidOperationException("Unable to read environment block data.");
            }

            return EnvToDictionary(envData);
        }

        private static StringDictionary EnvToDictionary(byte[] env)
        {
            var result = new StringDictionary();

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

            char[] environmentCharArray = Encoding.Unicode.GetChars(env, 0, len);

            for (int i = 0; i < environmentCharArray.Length; i++)
            {
                int startIndex = i;
                while ((environmentCharArray[i] != '=') && (environmentCharArray[i] != '\0'))
                {
                    i++;
                }
                if (environmentCharArray[i] != '\0')
                {
                    if ((i - startIndex) == 0)
                    {
                        while (environmentCharArray[i] != '\0')
                        {
                            i++;
                        }
                    }
                    else
                    {
                        string str = new string(environmentCharArray, startIndex, i - startIndex);
                        i++;
                        int num3 = i;
                        while (environmentCharArray[i] != '\0')
                        {
                            i++;
                        }
                        string str2 = new string(environmentCharArray, num3, i - num3);
                        result[str] = str2;
                    }
                }
            }

            return result;
        }

        private static bool TryReadIntPtr32(IntPtr hProcess, IntPtr ptr, out IntPtr readPtr)
        {
            bool result;
            RuntimeHelpers.PrepareConstrainedRegions();
            try
            {
            }
            finally
            {
                int dataSize = sizeof(Int32);
                var data = Marshal.AllocHGlobal(dataSize);
                IntPtr res_len = IntPtr.Zero;
                bool b = NativeMethods.ReadProcessMemory(
                    hProcess,
                    ptr,
                    data,
                    new IntPtr(dataSize),
                    ref res_len);
                readPtr = new IntPtr(Marshal.ReadInt32(data));
                Marshal.FreeHGlobal(data);
                if (!b || (int)res_len != dataSize)
                {
                    result = false;
                }
                else
                {
                    result = true;
                }
            }

            return result;
        }

        private static bool TryReadIntPtr(IntPtr hProcess, IntPtr ptr, out IntPtr readPtr)
        {
            bool result;
            RuntimeHelpers.PrepareConstrainedRegions();
            try
            {
            }
            finally
            {
                int dataSize = IntPtr.Size;
                var data = Marshal.AllocHGlobal(dataSize);
                IntPtr res_len = IntPtr.Zero;
                bool b = NativeMethods.ReadProcessMemory(
                    hProcess,
                    ptr,
                    data,
                    new IntPtr(dataSize),
                    ref res_len);

                readPtr = Marshal.ReadIntPtr(data);
                Marshal.FreeHGlobal(data);

                if (!b || (int)res_len != dataSize)
                {
                    result = false;
                }
                else
                {
                    result = true;
                }
            }

            return result;
        }

        private static IntPtr GetPenv(IntPtr hProcess)
        {
            int processBitness = GetProcessBitness(hProcess);

            if (processBitness == 64)
            {
                if (!System.Environment.Is64BitProcess)
                {
                    throw new InvalidOperationException(
                        "The current process should run in 64 bit mode to be able to get the environment of another 64 bit process.");
                }

                IntPtr pPeb = GetPeb64(hProcess);

                IntPtr ptr;
                if (!TryReadIntPtr(hProcess, pPeb + 0x20, out ptr))
                {
                    throw new InvalidOperationException("Unable to read PEB.");
                }

                IntPtr penv;
                if (!TryReadIntPtr(hProcess, ptr + 0x80, out penv))
                {
                    throw new InvalidOperationException("Unable to read RTL_USER_PROCESS_PARAMETERS.");
                }

                return penv;
            }
            else
            {
                IntPtr pPeb = GetPeb32(hProcess);

                IntPtr ptr;
                if (!TryReadIntPtr32(hProcess, pPeb + 0x10, out ptr))
                {
                    throw new InvalidOperationException("Unable to read PEB.");
                }

                IntPtr penv;
                if (!TryReadIntPtr32(hProcess, ptr + 0x48, out penv))
                {
                    throw new InvalidOperationException("Unable to read RTL_USER_PROCESS_PARAMETERS.");
                }

                return penv;
            }
        }

        private static int GetProcessBitness(IntPtr hProcess)
        {
            if (System.Environment.Is64BitOperatingSystem)
            {
                bool wow64;
                if (!NativeMethods.IsWow64Process(hProcess, out wow64))
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
                NativeMethods.NtQueryInformationProcess(
                    hProcess,
                    NativeMethods.ProcessWow64Information,
                    ref ptr,
                    pbiSize,
                    ref res_len);

                if (res_len != pbiSize)
                {
                    throw new InvalidOperationException("Unable to query process information.");
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
            var pbi = new NativeMethods.PROCESS_BASIC_INFORMATION();
            int res_len = 0;
            int pbiSize = Marshal.SizeOf(pbi);
            NativeMethods.NtQueryInformationProcess(
                hProcess,
                NativeMethods.ProcessBasicInformation,
                ref pbi,
                pbiSize,
                ref res_len);

            if (res_len != pbiSize)
            {
                throw new InvalidOperationException("Unable to query process information.");
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

            var memInfo = new NativeMethods.MEMORY_BASIC_INFORMATION();
            IntPtr result = NativeMethods.VirtualQueryEx(
                hProcess,
                address,
                ref memInfo,
                (IntPtr)Marshal.SizeOf(memInfo));

            if (result == IntPtr.Zero)
            {
                return false;
            }

            if (memInfo.Protect == NativeMethods.PAGE_NOACCESS || memInfo.Protect == NativeMethods.PAGE_EXECUTE)
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

        private static class NativeMethods
        {
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct PROCESS_BASIC_INFORMATION
            {
                public IntPtr Reserved1;
                public IntPtr PebBaseAddress;
                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
                public IntPtr[] Reserved2;
                public IntPtr UniqueProcessId;
                public IntPtr Reserved3;
            }

            public const int ProcessBasicInformation = 0;
            public const int ProcessWow64Information = 26;

            [DllImport("ntdll.dll", SetLastError = true)]
            public static extern int NtQueryInformationProcess(
                IntPtr hProcess,
                int pic,
                ref PROCESS_BASIC_INFORMATION pbi,
                int cb,
                ref int pSize);

            [DllImport("ntdll.dll", SetLastError = true)]
            public static extern int NtQueryInformationProcess(
                IntPtr hProcess,
                int pic,
                ref IntPtr pi,
                int cb,
                ref int pSize);

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
                IntPtr lpBuffer,
                IntPtr dwSize,
                ref IntPtr lpNumberOfBytesRead);

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
        }
    }
}