using Kudu.Contracts.SiteExtensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;

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
        public async Task<IEnumerable<SiteExtensionInfo>> GetRemoteExtensions(string filter = null, bool allowPrereleaseVersions = false, string feedUrl = null)
        {
            return await _manager.GetRemoteExtensions(filter, allowPrereleaseVersions, feedUrl);
        }

        [HttpGet]
        public async Task<SiteExtensionInfo> GetRemoteExtension(string id, string version = null, string feedUrl = null)
        {
            SiteExtensionInfo extension = await _manager.GetRemoteExtension(id, version, feedUrl);

            if (extension == null)
            {
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.NotFound, id));
            }

            return extension;
        }

        [HttpGet]
        public async Task<IEnumerable<SiteExtensionInfo>> GetLocalExtensions(string filter = null, bool checkLatest = true)
        {
            return await _manager.GetLocalExtensions(filter, checkLatest);
        }

        [HttpGet]
        public async Task<SiteExtensionInfo> GetLocalExtension(string id, bool checkLatest = true)
        {
            SiteExtensionInfo extension = await _manager.GetLocalExtension(id, checkLatest);
            if (extension == null)
            {
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.NotFound, id));
            }
            return extension;
        }

        [HttpPut]
        public async Task<SiteExtensionInfo> InstallExtension(string id, SiteExtensionInfo requestInfo)
        {
            if (requestInfo == null)
            {
                requestInfo = new SiteExtensionInfo();
            }

            SiteExtensionInfo extension;

            try
            {
                extension = await _manager.InstallExtension(id, requestInfo.Version, requestInfo.FeedUrl);
            }
            catch (WebException e)
            {
                // This can happen for example if a bad feed URL is passed
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Site extension download failure", e));
            }
            catch (Exception e)
            {
                // This can happen for example if the exception package is corrupted
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Site extension install exception. The package might be invalid.", e));
            }

            if (extension == null)
            {
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.NotFound, "Could not find " + id));
            }

            return extension;
        }

        [HttpDelete]
        public async Task<bool> UninstallExtension(string id)
        {
            try
            {
                return await _manager.UninstallExtension(id);
            }
            catch (DirectoryNotFoundException ex)
            {
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.NotFound, ex));
            }
        }
    }
}
