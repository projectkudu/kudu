using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace Kudu.Core.Infrastructure
{
    public static class HandleUtility
    {
        public static IEnumerable<HandleInfo> GetHandles(int processId)
        {
            uint length = 0x10000;
            IntPtr ptr = IntPtr.Zero;
            try
            {
                while (true)
                {
                    ptr = Marshal.AllocHGlobal((int) length);
                    uint returnLength;
                    var result = 
                        FileHandleNativeMethods.NtQuerySystemInformation(
                        SYSTEM_INFORMATION_CLASS.SystemHandleInformation, ptr, length, out returnLength);

                    if (result == NTSTATUS.STATUS_INFO_LENGTH_MISMATCH)
                    {
                        // Round required memory up to the nearest 64KB boundary.
                        length = ((returnLength + 0xffff) & ~(uint)0xffff);
                    }
                    else if (result == NTSTATUS.STATUS_SUCCESS)
                    {
                        break;
                    }
                }

                int handleCount = IntPtr.Size == 4 ? Marshal.ReadInt32(ptr) : (int)Marshal.ReadInt64(ptr);
                int offset = IntPtr.Size;
                int size = Marshal.SizeOf(typeof(SYSTEM_HANDLE_ENTRY));
                for (int i = 0; i < handleCount; i++)
                {
                    var handleEntry = 
                        (SYSTEM_HANDLE_ENTRY)Marshal.PtrToStructure(
                        IntPtr.Add(ptr, offset), typeof(SYSTEM_HANDLE_ENTRY));

                    if (handleEntry.OwnerProcessId == processId)
                    {
                        yield return new HandleInfo(
                            handleEntry.OwnerProcessId,
                            handleEntry.Handle,
                            handleEntry.GrantedAccess,
                            handleEntry.ObjectTypeNumber);
                    }

                    offset += size;
                }
            }
            finally
            {
                if (ptr != IntPtr.Zero)
                    Marshal.FreeHGlobal(ptr);
            }
        }
    }
}
