using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web.Http;
using Ionic.Zip;
using Kudu.Services.Infrastructure;

namespace Kudu.Services.Performance
{
    public class DiagnosticsController : ApiController
    {
        private readonly string[] _paths;
        private static object _lockObj = new object();

        public DiagnosticsController(params string[] paths)
        {
            _paths = paths;
        }

        /// <summary>
        /// Get all the diagnostic logs as a zip file
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public HttpResponseMessage GetLog()
        {
            lock (_lockObj)
            {
                HttpResponseMessage response = Request.CreateResponse();
                using (var zip = new ZipFile())
                {
                    foreach (var path in _paths)
                    {
                        if (Directory.Exists(path))
                        {
                            zip.AddDirectory(path, Path.GetFileName(path));
                        }
                        else if (File.Exists(path))
                        {
                            zip.AddFile(path, String.Empty);
                        }
                    }

                    var ms = new MemoryStream();
                    zip.Save(ms);
                    response.Content = ms.AsContent();
                }
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
                response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment");
                response.Content.Headers.ContentDisposition.FileName = String.Format("dump-{0:MM-dd-H:mm:ss}.zip", DateTime.UtcNow);
                return response;
            }
        }
    }
}
