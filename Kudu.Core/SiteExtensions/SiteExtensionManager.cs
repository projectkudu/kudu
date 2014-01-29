using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Kudu.Contracts.SiteExtensions;

namespace Kudu.Core.SiteExtensions
{
    public class SiteExtensionManager : ISiteExtensionManager
    {
        // TODO, suwatch: testing purpose
        static SiteExtensionInfo DummyInfo = new SiteExtensionInfo
        {
            Name = "Dummy Extension",
            Description = "Dummy stuff",
            Id = "Dummy",
            Update = new SiteExtensionInfo
            {
                Id = "Dummy",
            }
        };

        static SiteExtensionInfo CoolInfo0 = new SiteExtensionInfo
        {
            Name = "Cool Extension 0",
            Description = "Do cool stuff",
            Id = "Cool",
            Update = new SiteExtensionInfo
            {
                Id = "Cool",
            }
        };

        static SiteExtensionInfo CoolInfo1 = new SiteExtensionInfo
        {
            Name = "Cool Extension 1",
            Description = "Do cool stuff",
            Id = "Cool",
            Update = new SiteExtensionInfo
            {
                Id = "Cool",
            }
        };

        static SiteExtensionInfo CoolInfo2 = new SiteExtensionInfo
        {
            Name = "Cool Extension 2",
            Description = "Do cool stuff",
            Id = "Cool",
            Update = new SiteExtensionInfo
            {
                Id = "Cool",
            }
        };

        static SiteExtensionInfo CoolInfo3 = new SiteExtensionInfo
        {
            Name = "Cool Extension 3",
            Description = "Do cool stuff",
            Id = "Cool",
            Update = new SiteExtensionInfo
            {
                Id = "Cool",
            }
        };

        public async Task<IEnumerable<SiteExtensionInfo>> GetRemoteExtensions(string filter, string version)
        {
            return await Task.FromResult(new[] { DummyInfo, CoolInfo0, CoolInfo1, CoolInfo2, CoolInfo3 });
        }

        public async Task<SiteExtensionInfo> GetRemoteExtension(string id, string version)
        {
            return await Task.FromResult(DummyInfo);
        }

        public async Task<IEnumerable<SiteExtensionInfo>> GetLocalExtensions(string filter, bool update_info)
        {
            return await Task.FromResult(new[] { DummyInfo });
        }

        public async Task<SiteExtensionInfo> GetLocalExtension(string id, bool update_info)
        {
            return await Task.FromResult(DummyInfo);
        }

        public async Task<SiteExtensionInfo> InstallExtension(SiteExtensionInfo info)
        {
            return await Task.FromResult(info);
        }

        public async Task<bool> UninstallExtension(string id)
        {
            return await Task.FromResult(true);
        }
    }
}
