using System.Net;
using System.Net.Http;
using System.Web.Http;
using Newtonsoft.Json.Linq;

namespace Kudu.Services
{
    public class EnvironmentController : ApiController
    {
        private static readonly string _version = typeof(EnvironmentController).Assembly.GetName().Version.ToString();

        /// <summary>
        /// Get the Kudu version
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public HttpResponseMessage Get()
        {
            // Return the version and other api information (in the end)
            // { 
            //   "version" : "1.0.0"
            // }
            var obj = new JObject(new JProperty("version", _version));
            return Request.CreateResponse(HttpStatusCode.OK, obj);
        }
    }
}
