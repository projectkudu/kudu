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
            return Request.CreateResponse(HttpStatusCode.NotFound, "No route registered for '" + Request.RequestUri.PathAndQuery + "'");
        }
    }
}