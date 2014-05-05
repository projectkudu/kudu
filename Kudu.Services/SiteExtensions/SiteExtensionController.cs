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
        public IEnumerable<SiteExtensionInfo> GetLocalExtensions(string filter = null, bool checkLatest = true)
        {
            return _manager.GetLocalExtensions(filter, checkLatest);
        }

        [HttpGet]
        public SiteExtensionInfo GetLocalExtension(string id, bool checkLatest = true)
        {
            SiteExtensionInfo extension = _manager.GetLocalExtension(id, checkLatest);
            if (extension == null)
            {
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.NotFound, id));
            }
            return extension;
        }

        [HttpPut]
        public SiteExtensionInfo InstallExtension(string id, string version = null)
        {
            SiteExtensionInfo extension = _manager.InstallExtension(id, version);
            if (extension == null)
            {
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.NotFound, id));
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
