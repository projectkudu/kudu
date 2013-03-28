using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Kudu.SiteManagement
{
    internal class IdleManager
    {
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "This mirrors the signature of Kudu.Core.IdleManager")]
        public void UpdateActivity()
        {
        }

        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "This mirrors the signature of Kudu.Core.IdleManager")]
        public void WaitForExit(Process process)
        {
            process.WaitForExit();
        }
    }
}
