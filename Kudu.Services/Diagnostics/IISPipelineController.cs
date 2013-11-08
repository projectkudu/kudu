using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Kudu.Contracts.Tracing;
using Kudu.Core.Diagnostics;


namespace Kudu.Services.Performance
{
    public class IISPipelineController : ApiController
    {
        private readonly ITracer _tracer;

        public IISPipelineController(ITracer tracer)
        {
            _tracer = tracer;
        }

        [HttpGet]
        public HttpResponseMessage GetIISPipelineInfo()
        {
            using (_tracer.Step("IISPipelineController.GetIISPipelineInfo"))
            {
                var serverVariables = System.Web.HttpContext.Current.Request.ServerVariables;
                var info = new IISPipelineInfo
                {
                    TotalServedRequestsCount = ValueOrDefault(() => Int32.Parse(serverVariables[Constants.TotalRequestCountHeader]), -1),
                    ActiveRequestsCount = ValueOrDefault(() => Int32.Parse(serverVariables[Constants.ActiveRequestsCountHeader]), -1),
                    ListActiveRequests = ValueOrDefault(() => serverVariables[Constants.ListActiveRequestsHeader].Split(';').Select(s => s.Trim()).Where(s => !String.IsNullOrEmpty(s)).ToList(), new List<string>())
                };

                return Request.CreateResponse(HttpStatusCode.OK, info);
            }
        }

        private static T ValueOrDefault<T>(Func<T> value, T Default)
        {
            try
            {
                return value.Invoke();
            }
            catch (Exception)
            {
            }

            return Default;
        }

    }
}
