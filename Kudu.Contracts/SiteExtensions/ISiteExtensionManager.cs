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

        /// <summary>
        /// Install or update a site extension.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="version"></param>
        /// <param name="feedUrl"></param>
        /// <param name="type"></param>
        /// <param name="tracer"></param>
        /// <param name="installationArgs">String argument to pass to the install.cmd script. If the string is "a b" then the batch script will parse as %1 = a, %2 = b</param>
        Task<SiteExtensionInfo> InstallExtension(string id, string version, string feedUrl, SiteExtensionInfo.SiteExtensionType type, ITracer tracer, string installationArgs);

        Task<bool> UninstallExtension(string id);
    }
}
