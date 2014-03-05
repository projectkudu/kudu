using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
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
        public IEnumerable<SiteExtensionInfo> GetRemoteExtensions(string filter = null, bool allowPrereleaseVersions = false)
        {
            return _manager.GetRemoteExtensions(filter, allowPrereleaseVersions);
        }

        [HttpGet]
        public SiteExtensionInfo GetRemoteExtension(string id, string version = null)
        {
            SiteExtensionInfo extension = _manager.GetRemoteExtension(id, version);
            if (extension == null)
            {
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.NotFound, id));
            }
            return extension;
        }

        [HttpGet]
        public IEnumerable<SiteExtensionInfo> GetLocalExtensions(string filter = null, bool latestInfo = false)
        {
            return _manager.GetLocalExtensions(filter, latestInfo);
        }

        [HttpGet]
        public SiteExtensionInfo GetLocalExtension(string id, bool latestInfo = false)
        {
            SiteExtensionInfo extension = _manager.GetLocalExtension(id, latestInfo);
            if (extension == null)
            {
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.NotFound, id));
            }
            return extension;
        }

        [HttpPost]
        public SiteExtensionInfo InstallExtension(SiteExtensionInfo info)
        {
            SiteExtensionInfo extension = _manager.InstallExtension(info);
            if (extension == null)
            {
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.NotFound, info.ToString()));
            }
            return extension;
        }

        [HttpDelete]
        public bool UninstallExtension(string id)
        {
            try
            {
                return _manager.UninstallExtension(id);
            }
            catch (DirectoryNotFoundException ex)
            {
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.NotFound, ex));
            }
        }
    }
}
