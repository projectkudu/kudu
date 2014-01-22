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
        public async Task<IEnumerable<SiteExtensionInfo>> GetExtensions(string filter = null)
        {
            return await _manager.GetExtensions(filter);
        }

        [HttpGet]
        public async Task<SiteExtensionInfo> GetExtension(string id)
        {
            return await _manager.GetExtension(id);
        }

        [HttpPut]
        public async Task<SiteExtensionInfo> InstallExtension(SiteExtensionInfo info)
        {
            return await _manager.InstallExtension(info);
        }

        [HttpPost]
        public async Task<SiteExtensionInfo> UpdateExtension(SiteExtensionInfo info)
        {
            return await _manager.UpdateExtension(info);
        }

        [HttpDelete]
        public async Task<bool> UninstallExtension(string id)
        {
            return await _manager.UninstallExtension(id);
        }
    }
}
