using System.Collections.Generic;
using System.Threading.Tasks;
using Kudu.Contracts.Tracing;

namespace Kudu.Contracts.SiteExtensions
{
    public interface ISiteExtensionManager
    {
        Task<IEnumerable<SiteExtensionInfo>> GetRemoteExtensions(string filter, bool allowPrereleaseVersions, string feedUrl);

        Task<SiteExtensionInfo> GetRemoteExtension(string id, string version, string feedUrl);

        Task<IEnumerable<SiteExtensionInfo>> GetLocalExtensions(string filter, bool checkLatest);

        Task<SiteExtensionInfo> GetLocalExtension(string id, bool checkLatest);

        Task<SiteExtensionInfo> InstallExtension(string id, SiteExtensionInfo requestInfo, ITracer tracer);

        Task<bool> UninstallExtension(string id);
    }
}
