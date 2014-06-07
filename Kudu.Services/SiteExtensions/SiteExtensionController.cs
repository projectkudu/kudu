using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Web.Http;
using Kudu.Contracts.SiteExtensions;
using Kudu.Core.Infrastructure;
using Newtonsoft.Json;

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
            try
            {
                SiteExtensionInfo extension = _manager.InstallExtension(id, version);
                if (extension == null)
                {
                    throw new Exception("Install process failed.");
                }
                return extension;
            }
            catch (Exception ex)
            {
                var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent(JsonConvert.SerializeObject(new
                    {
                        Message = ex.Message.Replace(Environment.NewLine, " ")
                    }))
                };
                throw new HttpResponseException(response);
            }
        }

        [HttpDelete]
        public bool UninstallExtension(string id)
        {
            return TryOperation(() =>
            {
                bool success = _manager.UninstallExtension(id);
                if (!success)
                {
                    throw new Exception("Uninstall process is not complete.");
                }
                return true;
            });
        }

        private T TryOperation<T>(Func<T> func)
        {
            try
            {
                return func();
            }
            catch (CommandLineException ex)
            {
                var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent(JsonConvert.SerializeObject(new
                    {
                        Message = ex.Error.Replace(Environment.NewLine, " ")
                    }))
                };
                throw new HttpResponseException(response);
            }
            catch (Exception ex)
            {
                var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent(JsonConvert.SerializeObject(new
                    {
                        Message = ex.Message.Replace(Environment.NewLine, " ")
                    }))
                };
                throw new HttpResponseException(response);
            }
        }
    }
}
