using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Security;

namespace Kudu.Core.Infrastructure
{
    [SuppressUnmanagedCodeSecurity]
    internal static class MiniDumpNativeMethods
    {
        [DllImport("dbghelp.dll",
            EntryPoint = "MiniDumpWriteDump",
            CallingConvention = CallingConvention.StdCall,
            CharSet = CharSet.Unicode,
            ExactSpelling = true,
            SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool MiniDumpWriteDump(
            IntPtr hProcess,
            uint processId,
            SafeHandle hFile,
            MINIDUMP_TYPE dumpType,
            IntPtr expParam,
            IntPtr userStreamParam,
            IntPtr callbackParam);

        [DllImport("dbghelp.dll",
            EntryPoint = "MiniDumpWriteDump",
            CallingConvention = CallingConvention.StdCall,
            CharSet = CharSet.Unicode,
            ExactSpelling = true,
            SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool MiniDumpWriteDump(
            PssSnapshotSafeHandle hpss,
            uint processId,
            SafeHandle hFile,
            MINIDUMP_TYPE dumpType,
            IntPtr expParam,
            IntPtr userStreamParam,
            [In] ref MINIDUMP_CALLBACK_INFORMATION callbackParam);

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct MINIDUMP_CALLBACK_INFORMATION
        {
            public MINIDUMP_CALLBACK_ROUTINE CallbackRoutine;
            public IntPtr CallbackParam;
        }

        public enum MINIDUMP_CALLBACK_TYPE : uint
        {
            ReadMemoryFailureCallback = 14,
            IsProcessSnapshotCallback = 16
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct MINIDUMP_CALLBACK_INPUT_UNION
        {
            [FieldOffset(0)] public readonly uint Status;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct MINIDUMP_CALLBACK_INPUT
        {
            public readonly uint ProcessId;
            public readonly IntPtr ProcessHandle;
            public readonly MINIDUMP_CALLBACK_TYPE CallbackType;
            public readonly MINIDUMP_CALLBACK_INPUT_UNION CallbackUnion;
        }

        [StructLayout(LayoutKind.Explicit, Pack = 4, Size = 52)]
        public struct MINIDUMP_CALLBACK_OUTPUT
        {
            [FieldOffset(0)] public int Status;
        }

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public delegate bool MINIDUMP_CALLBACK_ROUTINE(IntPtr CallbackParam, [In] ref MINIDUMP_CALLBACK_INPUT CallbackInput, [In, Out] ref MINIDUMP_CALLBACK_OUTPUT CallbackOutput);
    }

    [Flags]
    [SuppressMessage("Microsoft.Design", "CA1008:EnumsShouldHaveZeroValue", Justification = "")]
    [SuppressMessage("Microsoft.Naming", "CA1714:FlagsEnumsShouldHavePluralNames", Justification = "")]
    public enum MINIDUMP_TYPE : int
    {
        // From dbghelp.h:
        Normal                          = 0x00000000,
        WithDataSegs                    = 0x00000001,
        WithFullMemory                  = 0x00000002,
        WithHandleData                  = 0x00000004,
        FilterMemory                    = 0x00000008,
        ScanMemory                      = 0x00000010,
        WithUnloadedModules             = 0x00000020,
        WithIndirectlyReferencedMemory  = 0x00000040,
        FilterModulePaths               = 0x00000080,
        WithProcessThreadData           = 0x00000100,
        WithPrivateReadWriteMemory      = 0x00000200,
        WithoutOptionalData             = 0x00000400,
        WithFullMemoryInfo              = 0x00000800,
        WithThreadInfo                  = 0x00001000,
        WithCodeSegs                    = 0x00002000,
        WithoutAuxiliaryState           = 0x00004000,
        WithFullAuxiliaryState          = 0x00008000,
        WithPrivateWriteCopyMemory      = 0x00010000,
        IgnoreInaccessibleMemory        = 0x00020000,
        [SuppressMessage("Microsoft.Naming", "CA1726:UsePreferredTerms", Justification = "")]
        ValidTypeFlags                  = 0x0003ffff,
    };
}