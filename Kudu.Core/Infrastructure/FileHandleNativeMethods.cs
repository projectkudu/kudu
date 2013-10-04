using System;
using System.IO;
using System.Runtime.ConstrainedExecution;
using System.Text;
using System.Runtime.InteropServices;

namespace Kudu.Core.Infrastructure
{
    enum NtStatus : uint
    {
        StatusSuccess = 0x0,
        StatusInfoLengthMismatch = 0xC0000004
    }

    enum SystemInformationClass
    {
        SystemHandleInformation = 16
    }

    enum ObjectInformationClass
    {
        ObjectNameInformation = 1,
        ObjectTypeInformation = 2
    }

    enum FileFlagsAndAttributes : uint
    {
        FileFlagBackupSemantics = 0x02000000
    }

    enum ProcessAccessRights : uint
    {
        ProcessDupHandle = 0x0040
    }

    enum DuplicateHandleOptions : uint
    {
        DuplicateSameAccess = 0x00000002
    }

    [StructLayout(LayoutKind.Sequential)]
    struct SystemHandleEntry
    {
        internal uint OwnerProcessId;
        internal byte ObjectTypeNumber;
        internal byte Flags;
        internal ushort Handle;
        internal IntPtr Object;
        internal int GrantedAccess;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct UnicodeString : IDisposable
    {
        internal ushort Length;
        internal ushort MaximumLength;
        internal IntPtr buffer;

        public void Dispose()
        {
            Marshal.FreeHGlobal(buffer);
            buffer = IntPtr.Zero;
        }

        public override string ToString()
        {
            return Marshal.PtrToStringUni(buffer);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct GenericMapping
    {
        internal uint GenericRead;
        internal uint GenericWrite;
        internal uint GenericExecute;
        internal uint GenericAll;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct ObjectTypeInformation
    {
        internal UnicodeString Name;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 22, ArraySubType = UnmanagedType.U4)]
        internal uint[] Reserved;
    }

    public static class FileHandleNativeMethods
    {
        [DllImport("ntdll.dll")]
        internal static extern NtStatus NtQuerySystemInformation(
            [In] SystemInformationClass systemInformationClass,
            [In] IntPtr systemInformation,
            [In] uint systemInformationLength,
            [Out] out uint returnLength);

        [DllImport("ntdll.dll")]
        internal static extern NtStatus NtQueryObject(
            [In] IntPtr handle,
            [In] ObjectInformationClass objectInformationClass,
            [In] IntPtr objectInformation,
            [In] uint objectInformationLength,
            [Out] out uint returnLength);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern int QueryDosDevice(
            [In] string deviceName,
            [Out] StringBuilder targetPath,
            [In] uint max);

        [DllImport("kernel32.dll")]
        internal static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr OpenProcess(
            [In] ProcessAccessRights desiredAccess,
            [In, MarshalAs(UnmanagedType.Bool)] bool inheritHandle,
            [In] uint processId);

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CloseHandle(
            [In] IntPtr objectHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DuplicateHandle(
            [In] IntPtr sourceProcessHandle,
            [In] IntPtr sourceHandle,
            [In] IntPtr sargetProcessHandle,
            [Out] out IntPtr targetHandle,
            [In] uint desiredAccess,
            [In, MarshalAs(UnmanagedType.Bool)] bool inheritHandle,
            [In] DuplicateHandleOptions options);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern IntPtr CreateFile(
            [In] string filename,
            [In] FileAccess access,
            [In] FileShare share,
            [In] IntPtr securityAttributes,
            [In] FileMode creationDisposition,
            [In] FileFlagsAndAttributes flagsAndAttributes,
            [In] IntPtr templateFile);
    }
}
