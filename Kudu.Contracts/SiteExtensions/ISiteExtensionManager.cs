using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kudu.Contracts.SiteExtensions
{
    public interface ISiteExtensionManager
    {
        // TODO, suwatch: Specific filter for Online, Installed and Update available
        // TODO, suwatch: Unbounded site extension items, paging support?
        // TODO, suwatch: Partial name match
        Task<IEnumerable<SiteExtensionInfo>> GetExtensions(string filter);

        Task<SiteExtensionInfo> GetExtension(string id);

        // TODO, suwatch: should we combine install/update into one - CreateOrUpdate etc?
        Task<SiteExtensionInfo> InstallExtension(SiteExtensionInfo info);

        Task<SiteExtensionInfo> UpdateExtension(SiteExtensionInfo info);

        Task<bool> UninstallExtension(string id);
    }
}
