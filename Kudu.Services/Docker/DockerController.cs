using Kudu.Core.Helpers;
using Kudu.Core.Tracing;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace Kudu.Services.Docker
{
    public class DockerController : ApiController
    {
        const string TimestampDirectory = "/home/config";
        ITraceFactory _traceFactory;

        public DockerController(ITraceFactory traceFactory)
        {
            _traceFactory = traceFactory;
        }

        [HttpPost]
        public HttpResponseMessage ReceiveHook()
        {
            if (OSDetector.IsOnWindows())
            {
                return Request.CreateResponse(HttpStatusCode.NotFound);
            }

            var tracer = _traceFactory.GetTracer();
            using (tracer.Step("Docker.SetDockerTimestamp"))
            {
                string timestampPath = Path.Combine(TimestampDirectory, "dockerTimestamp.txt");
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName((timestampPath)));
                    File.WriteAllText(timestampPath, DateTime.UtcNow.ToString());
                }
                catch (Exception e)
                {
                    return Request.CreateResponse(HttpStatusCode.InternalServerError, e.Message);
                }

                return Request.CreateResponse(HttpStatusCode.OK);
            }
        }
    }
}
