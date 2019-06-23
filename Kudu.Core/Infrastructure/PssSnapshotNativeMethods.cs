using System;
using System.Runtime.InteropServices;
using System.Security;

namespace Kudu.Core.Infrastructure
{
    [SuppressUnmanagedCodeSecurity]
    internal static class PssSnapshotNativeMethods
    {
        public enum PSS_QUERY_INFORMATION_CLASS
        {
            PSS_QUERY_VA_CLONE_INFORMATION = 1,
        }

        [Flags]
        public enum PSS_CAPTURE_FLAGS : uint
        {
            PSS_CAPTURE_NONE = 0x00000000,
            PSS_CAPTURE_VA_CLONE = 0x00000001,
            PSS_CAPTURE_THREADS = 0x00000080,
            PSS_CAPTURE_THREAD_CONTEXT = 0x00000100,
            PSS_CAPTURE_VA_SPACE = 0x00000800,
            PSS_CAPTURE_VA_SPACE_SECTION_INFORMATION = 0x00001000,
            PSS_CREATE_USE_VM_ALLOCATIONS = 0x20000000
        }

        public struct PSS_VA_CLONE_INFORMATION
        {
            public IntPtr VaCloneHandle;
        }

        [DllImport(c_Kernel32)]
        public static extern int PssCaptureSnapshot(IntPtr processHandle, PSS_CAPTURE_FLAGS captureFlags, uint threadContextFlags, out PssSnapshotSafeHandle snapshotHandle);

        [DllImport(c_Kernel32)]
        public static extern int PssFreeSnapshot(IntPtr processHandle, IntPtr snapshotHandle);

        [DllImport(c_Kernel32)]
        public static extern int PssQuerySnapshot(IntPtr snapshotHandle, PSS_QUERY_INFORMATION_CLASS informationClass, out PSS_VA_CLONE_INFORMATION vaCloneInformation, int bufferLength);

        private const string c_Kernel32 = "kernel32";
    }
}
