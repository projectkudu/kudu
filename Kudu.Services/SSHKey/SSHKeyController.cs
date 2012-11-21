using System;
using System.IO.Abstractions;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.Tracing;
using Kudu.Core.SSHKey;
using Newtonsoft.Json.Linq;

namespace Kudu.Services.SSHKey
{
    public class SSHKeyController : ApiController
    {
        private const string KeyParameterName = "key";

        private readonly ITracer _tracer;
        private readonly ISSHKeyManager _sshKeyManager;
        private readonly IFileSystem _fileSystem;
        private readonly IOperationLock _sshKeyLock;

        public SSHKeyController(ITracer tracer, ISSHKeyManager sshKeyManager, IFileSystem fileSystem, IOperationLock sshKeyLock)
        {
            _tracer = tracer;
            _sshKeyManager = sshKeyManager;
            _fileSystem = fileSystem;
            _sshKeyLock = sshKeyLock;
        }

        
        /// <summary>
        /// Set the private key. The supported key format is privacy enhanced mail (PEM)
        /// </summary>
        [HttpPut]
        public void SetPrivateKey()
        {
            string key;
            if (IsContentType("application/json"))
            {
                JObject result = GetJsonContent();
                key = result == null ? null : result.Value<string>(KeyParameterName);
            }
            else
            {
                // any other content-type assuming the content is key
                // curl http://server/sshkey -X PUT --upload-file /c/temp/id_rsa
                key = Request.Content.ReadAsStringAsync().Result;
            }

            if (String.IsNullOrEmpty(key))
            {
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, new ArgumentNullException(KeyParameterName)));
            }

            using (_tracer.Step("SSHKeyController.SetPrivateKey"))
            {
                _sshKeyLock.LockOrWait(() =>
                {
                    try
                    {
                        _sshKeyManager.SetPrivateKey(key);
                    }
                    catch (ArgumentException ex)
                    {
                        throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, ex));
                    }
                    catch (InvalidOperationException ex)
                    {
                        throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.Conflict, ex));
                    }
                }, TimeSpan.FromSeconds(5));
            }
        }

        private bool IsContentType(string mediaType)
        {
            var contentType = Request.Content.Headers.ContentType;
            if (contentType == null)
            {
                return false;
            }

            return contentType.MediaType != null &&
                contentType.MediaType.StartsWith(mediaType, StringComparison.OrdinalIgnoreCase);
        }

        private JObject GetJsonContent()
        {
            try
            {
                return Request.Content.ReadAsAsync<JObject>().Result;
            }
            catch
            {
                // We're going to return null here since we don't want to force a breaking change
                // on the client side. If the incoming request isn't application/json, we want this 
                // to return null.
                return null;
            }
        }
    }
}
