using System;
using System.Collections.Generic;

namespace Kudu.Contracts.SiteExtensions
{
    public interface ISiteExtensionManager
    {
        IEnumerable<SiteExtensionInfo> GetRemoteExtensions(string filter, bool allowPrereleaseVersions);

        SiteExtensionInfo GetRemoteExtension(string id, string version);

        IEnumerable<SiteExtensionInfo> GetLocalExtensions(string filter, bool checkLatest);

        SiteExtensionInfo GetLocalExtension(string id, bool checkLatest);

        SiteExtensionInfo InstallExtension(string id, string version = null);

        bool UninstallExtension(string id);
    }
}
