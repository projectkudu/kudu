using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web.Http;
using Kudu.Contracts.SiteExtensions;

namespace Kudu.Services.SiteExtensions
{
    public class SiteExtensionController : ApiController
    {
        private readonly ISiteExtensionManager _manager;

        public SiteExtensionController(ISiteExtensionManager manager)
        {
            _manager = manager;
        }

        [HttpGet]
        public async Task<IEnumerable<SiteExtensionInfo>> GetRemoteExtensions(string filter = null, string version = null)
        {
            return await _manager.GetRemoteExtensions(filter, version);
        }

        [HttpGet]
        public async Task<SiteExtensionInfo> GetRemoteExtension(string id, string version = null)
        {
            return await _manager.GetRemoteExtension(id, version);
        }

        [HttpGet]
        public async Task<IEnumerable<SiteExtensionInfo>> GetLocalExtensions(string filter = null, bool update_info = true)
        {
            return await _manager.GetLocalExtensions(filter, update_info);
        }

        [HttpGet]
        public async Task<SiteExtensionInfo> GetLocalExtension(string id, bool update_info = true)
        {
            return await _manager.GetLocalExtension(id, update_info);
        }

        [HttpPost]
        public async Task<SiteExtensionInfo> InstallExtension(SiteExtensionInfo info)
        {
            return await _manager.InstallExtension(info);
        }

        [HttpDelete]
        public async Task<bool> UninstallExtension(string id)
        {
            return await _manager.UninstallExtension(id);
        }
    }
}
