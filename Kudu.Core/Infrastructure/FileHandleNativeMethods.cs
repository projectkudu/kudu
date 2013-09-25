using System;
using System.Runtime.ConstrainedExecution;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace Kudu.Core.Infrastructure
{
    enum NT_STATUS
    {
        STATUS_SUCCESS = 0x00000000,
        STATUS_BUFFER_OVERFLOW = unchecked((int)0x80000005L),
        STATUS_INFO_LENGTH_MISMATCH = unchecked((int)0xC0000004L)
    }

    enum SYSTEM_INFORMATION_CLASS
    {
        SystemBasicInformation = 0,
        SystemPerformanceInformation = 2,
        SystemTimeOfDayInformation = 3,
        SystemProcessInformation = 5,
        SystemProcessorPerformanceInformation = 8,
        SystemHandleInformation = 16,
        SystemInterruptInformation = 23,
        SystemExceptionInformation = 33,
        SystemRegistryQuotaInformation = 37,
        SystemLookasideInformation = 45
    }

    enum OBJECT_INFORMATION_CLASS
    {
        ObjectBasicInformation = 0,
        ObjectNameInformation = 1,
        ObjectTypeInformation = 2,
        ObjectAllTypesInformation = 3,
        ObjectHandleInformation = 4
    }

    public static class FileHandleNativeMethods
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct FILE_ID_BOTH_DIR_INFO
        {
            public uint NextEntryOffset;
            public uint FileIndex;
            public LARGE_INTEGER CreationTime;
            public LARGE_INTEGER LastAccessTime;
            public LARGE_INTEGER LastWriteTime;
            public LARGE_INTEGER ChangeTime;
            public LARGE_INTEGER EndOfFile;
            public LARGE_INTEGER AllocationSize;
            public uint FileAttributes;
            public uint FileNameLength;
            public uint EaSize;
            public char ShortNameLength;
            [MarshalAsAttribute(UnmanagedType.ByValTStr, SizeConst = 12)]
            public string ShortName;
            public LARGE_INTEGER FileId;
            [MarshalAsAttribute(UnmanagedType.ByValTStr, SizeConst = 1)]
            public string FileName;
        }

        [StructLayout(LayoutKind.Explicit, Size = 8)]
        internal struct LARGE_INTEGER
        {
            [FieldOffset(0)]
            public Int64 QuadPart;
            [FieldOffset(0)]
            public UInt32 LowPart;
            [FieldOffset(4)]
            public Int32 HighPart;
        }

        internal enum FILE_INFO_BY_HANDLE_CLASS
        {
            FileIdBothDirectoryInfo = 10
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool GetFileInformationByHandleEx(
            IntPtr hFile,
            FILE_INFO_BY_HANDLE_CLASS infoClass,
            out FILE_ID_BOTH_DIR_INFO dirInfo,
            uint dwBufferSize);

        [DllImport("ntdll.dll")]
        internal static extern NT_STATUS NtQuerySystemInformation(
            [In] SYSTEM_INFORMATION_CLASS SystemInformationClass,
            [In] IntPtr SystemInformation,
            [In] int SystemInformationLength,
            [Out] out int ReturnLength);

        [DllImport("ntdll.dll")]
        internal static extern NT_STATUS NtQueryObject(
            [In] IntPtr Handle,
            [In] OBJECT_INFORMATION_CLASS ObjectInformationClass,
            [In] IntPtr ObjectInformation,
            [In] int ObjectInformationLength,
            [Out] out int ReturnLength);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern int QueryDosDevice(
            [In] string lpDeviceName,
            [Out] StringBuilder lpTargetPath,
            [In] int ucchMax);

        [DllImport("kernel32.dll")]
        internal static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr OpenProcess(
            [In] int dwDesiredAccess,
            [In, MarshalAs(UnmanagedType.Bool)] bool bInheritHandle,
            [In] int dwProcessId);

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CloseHandle(
            [In] IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DuplicateHandle(
            [In] IntPtr hSourceProcessHandle,
            [In] IntPtr hSourceHandle,
            [In] IntPtr hTargetProcessHandle,
            [Out] out IntPtr lpTargetHandle,
            [In] int dwDesiredAccess,
            [In, MarshalAs(UnmanagedType.Bool)] bool bInheritHandle,
            [In] int dwOptions);
    }
}
