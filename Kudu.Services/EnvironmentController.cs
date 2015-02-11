using System.IO;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Kudu.Core;
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
            //   "version" : "1.0.0",
            //   "siteLastModified" : "2015-02-11T18:27:22.7270000Z",
            // }
            var obj = new JObject(new JProperty("version", _version));

            // this file is written by dwas to communicate the last site configuration modified time
            var lastModifiedFile = Path.Combine(
                Path.GetDirectoryName(System.Environment.GetEnvironmentVariable("TMP")), 
                @"config\SiteLastModifiedTime.txt");
            if (File.Exists(lastModifiedFile))
            {
                obj["siteLastModified"] = File.ReadAllText(lastModifiedFile);
            }

            return Request.CreateResponse(HttpStatusCode.OK, obj);
        }
    }
}
