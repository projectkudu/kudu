using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Kudu.Contracts.SiteExtensions;
using Newtonsoft.Json.Linq;

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
        public IEnumerable<SiteExtensionInfo> GetRemoteExtensions(string filter = null, bool allowPrereleaseVersions = false, string feedUrl = null)
        {
            return _manager.GetRemoteExtensions(filter, allowPrereleaseVersions, feedUrl);
        }

        [HttpGet]
        public SiteExtensionInfo GetRemoteExtension(string id, string version = null, string feedUrl = null)
        {
            SiteExtensionInfo extension = _manager.GetRemoteExtension(id, version, feedUrl);

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
        public SiteExtensionInfo InstallExtension(string id, SiteExtensionInfo requestInfo)
        {
            if (requestInfo == null)
            {
                requestInfo = new SiteExtensionInfo();
            }

            SiteExtensionInfo extension;

            try
            {
                extension = _manager.InstallExtension(id, requestInfo.Version, requestInfo.FeedUrl);
            }
            catch (WebException e)
            {
                // This can happen for example if a bad feed URL is passed
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Site extension download failure", e));
            }

            if (extension == null)
            {
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.NotFound, "Could not find " + id));
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
