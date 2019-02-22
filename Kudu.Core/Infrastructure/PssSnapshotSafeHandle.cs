using Microsoft.Win32.SafeHandles;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using static Kudu.Core.Infrastructure.PssSnapshotNativeMethods;
using static Kudu.Core.Infrastructure.ProcessExtensions.ProcessNativeMethods;
using static System.Diagnostics.Process;

namespace Kudu.Core.Infrastructure
{
    /// <summary>
    /// A safe handle for process snapshots created via PssCaptureSnapshot.
    /// </summary>
    internal sealed class PssSnapshotSafeHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        /// <summary>
        /// This constructor will be called by interop.
        /// </summary>
        private PssSnapshotSafeHandle() : base(ownsHandle: true)
        {
        }

        /// <summary>
        /// Capture a snapshot of the given process.
        /// </summary>
        /// <param name="originalProcessHandle">The process to snapshot.</param>
        /// <returns>A safe handle to the captured snapshot.</returns>
        public static PssSnapshotSafeHandle CaptureSnapshot(IntPtr originalProcessHandle)
        {
            const PSS_CAPTURE_FLAGS captureFlags =
                PSS_CAPTURE_FLAGS.PSS_CAPTURE_VA_CLONE |
                PSS_CAPTURE_FLAGS.PSS_CAPTURE_VA_SPACE |
                PSS_CAPTURE_FLAGS.PSS_CAPTURE_VA_SPACE_SECTION_INFORMATION |
                PSS_CAPTURE_FLAGS.PSS_CAPTURE_THREADS |
                PSS_CAPTURE_FLAGS.PSS_CAPTURE_THREAD_CONTEXT |
                PSS_CAPTURE_FLAGS.PSS_CREATE_USE_VM_ALLOCATIONS;

            const uint CONTEXT_AMD64 = 0x00100000;
            const uint CONTEXT_CONTROL = CONTEXT_AMD64 | 0x00000001;
            const uint CONTEXT_INTEGER = CONTEXT_AMD64 | 0x00000002;
            const uint CONTEXT_SEGMENTS = CONTEXT_AMD64 | 0x00000004;
            const uint CONTEXT_FLOATING_POINT = CONTEXT_AMD64 | 0x00000008;
            const uint CONTEXT_DEBUG_REGISTERS = CONTEXT_AMD64 | 0x00000010;
            const uint CONTEXT_ALL = CONTEXT_CONTROL | CONTEXT_INTEGER | CONTEXT_SEGMENTS | CONTEXT_FLOATING_POINT | CONTEXT_DEBUG_REGISTERS;

            ThrowIfFailed(PssCaptureSnapshot(originalProcessHandle, captureFlags, CONTEXT_ALL, out var pssSnapshotHandle));
            return pssSnapshotHandle;
        }

        protected override bool ReleaseHandle()
        {
            // Kill the process before freeing the snapshot. Otherwise there's
            // a race with the OS which results in OpenProcess/TerminateProcess failing.
            var snapshotProcessId = GetSnapshotProcessId();
            TerminateProcessById(snapshotProcessId);

            ThrowIfFailed(PssFreeSnapshot(GetCurrentProcess().Handle, handle));
            return true;
        }

        private static void TerminateProcessById(uint processId)
        {
            // Note: We cannot use GetProcessById(processId).Kill() here.
            // GetProcessById will fail for snapshot processes in the sandbox.

            var handle = OpenProcess(PROCESS_TERMINATE, false, processId);
            if (handle == IntPtr.Zero)
            {
                throw new Win32Exception();
            }

            try
            {
                if (!TerminateProcess(handle, unchecked((uint)-1)))
                {
                    throw new Win32Exception();
                }
            }
            finally
            {
                CloseHandle(handle);
            }
        }

        /// <summary>
        /// Get the PID of the snapshot process.
        /// </summary>
        /// <returns>The process ID of the snapshot process.</returns>
        private uint GetSnapshotProcessId()
        {
            ThrowIfFailed(PssQuerySnapshot(handle, PSS_QUERY_INFORMATION_CLASS.PSS_QUERY_VA_CLONE_INFORMATION, out var vaCloneInformation, Marshal.SizeOf<PSS_VA_CLONE_INFORMATION>()));
            return GetProcessId(vaCloneInformation.VaCloneHandle);
        }

        private static void ThrowIfFailed(int status)
        {
            if (status != ERROR_SUCCESS)
            {
                throw new Win32Exception(status);
            }
        }

        private const int ERROR_SUCCESS = 0;
    }
}
