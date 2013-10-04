using System;
using System.IO;
using System.Runtime.ConstrainedExecution;
using System.Text;
using System.Runtime.InteropServices;

namespace Kudu.Core.Infrastructure
{
    enum NTSTATUS : uint
    {
        STATUS_SUCCESS = 0x0,
        STATUS_INFO_LENGTH_MISMATCH = 0xC0000004
    }

    enum SYSTEM_INFORMATION_CLASS
    {
        SystemHandleInformation = 16
    }

    enum OBJECT_INFORMATION_CLASS
    {
        ObjectNameInformation = 1,
        ObjectTypeInformation = 2
    }

    enum FILE_FLAGS_AND_ATTRIBUTES : uint
    {
        FILE_FLAG_BACKUP_SEMANTICS = 0x02000000
    }

    enum PROCESS_ACCESS_RIGHTS : uint
    {
        PROCESS_DUP_HANDLE = 0x0040
    }

    enum DUPLICATE_HANDLE_OPTIONS : uint
    {
        DUPLICATE_SAME_ACCESS = 0x00000002
    }

    [StructLayout(LayoutKind.Sequential)]
    struct SYSTEM_HANDLE_ENTRY
    {
        internal uint OwnerProcessId;
        internal byte ObjectTypeNumber;
        internal byte Flags;
        internal ushort Handle;
        internal IntPtr Object;
        internal int GrantedAccess;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct UNICODE_STRING : IDisposable
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
    struct GENERIC_MAPPING
    {
        internal uint GenericRead;
        internal uint GenericWrite;
        internal uint GenericExecute;
        internal uint GenericAll;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct PUBLIC_OBJECT_TYPE_INFORMATION
    {
        internal UNICODE_STRING TypeName;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 22, ArraySubType = UnmanagedType.U4)]
        internal uint[] Reserved;
    }

    public static class FileHandleNativeMethods
    {
        [DllImport("ntdll.dll")]
        internal static extern NTSTATUS NtQuerySystemInformation(
            [In] SYSTEM_INFORMATION_CLASS systemInformationClass,
            [In] IntPtr systemInformation,
            [In] uint systemInformationLength,
            [Out] out uint returnLength);

        [DllImport("ntdll.dll")]
        internal static extern NTSTATUS NtQueryObject(
            [In] IntPtr handle,
            [In] OBJECT_INFORMATION_CLASS objectInformationClass,
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
            [In] PROCESS_ACCESS_RIGHTS desiredAccess,
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
            [In] DUPLICATE_HANDLE_OPTIONS options);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern IntPtr CreateFile(
            [In] string filename,
            [In] FileAccess access,
            [In] FileShare share,
            [In] IntPtr securityAttributes,
            [In] FileMode creationDisposition,
            [In] FILE_FLAGS_AND_ATTRIBUTES flagsAndAttributes,
            [In] IntPtr templateFile);
    }
}
