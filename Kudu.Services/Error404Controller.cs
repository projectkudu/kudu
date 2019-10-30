using System;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace Kudu.Services
{
    public class Error404Controller : ApiController
    {
        [HttpGet, HttpPatch, HttpPost, HttpPut, HttpDelete]
        public HttpResponseMessage Handle()
        {
            // Mock few paths. For development purposes only.
            if (this.Request.RequestUri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            {
                if (this.Request.RequestUri.AbsolutePath.Equals(Constants.RestartApiPath, System.StringComparison.OrdinalIgnoreCase))
                {
                    return this.Request.CreateResponse(HttpStatusCode.OK);
                }
            }

            return Request.CreateResponse(HttpStatusCode.NotFound, "No route registered for '" + Request.RequestUri.PathAndQuery + "'");
        }
    }
}