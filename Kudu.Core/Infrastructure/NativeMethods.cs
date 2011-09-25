using System.Runtime.InteropServices;
using System.Text;

namespace Kudu.Core.Infrastructure {
    public static class NativeMethods {
        [DllImport("mpr.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int WNetGetConnection([MarshalAs(UnmanagedType.LPTStr)] string localName, [MarshalAs(UnmanagedType.LPTStr)] StringBuilder remoteName, ref int length);

        public class MappedDrive {
            public string LocalDriveLetter { get; set; }
            public string UncPath { get; set; }
        }
    }
}
