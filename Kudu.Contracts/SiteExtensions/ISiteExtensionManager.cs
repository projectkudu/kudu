using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kudu.Contracts.SiteExtensions
{
    public interface ISiteExtensionManager
    {
        Task<IEnumerable<SiteExtensionInfo>> GetRemoteExtensions(string filter, string version);

        Task<SiteExtensionInfo> GetRemoteExtension(string id, string version);

        Task<IEnumerable<SiteExtensionInfo>> GetLocalExtensions(string filter, bool update_info);

        Task<SiteExtensionInfo> GetLocalExtension(string id, bool update_info);

        Task<SiteExtensionInfo> InstallExtension(SiteExtensionInfo info);

        Task<bool> UninstallExtension(string id);
    }
}
