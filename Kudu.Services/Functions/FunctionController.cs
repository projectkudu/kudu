using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Kudu.Core.Functions;
using Kudu.Core.Tracing;
using Kudu.Services.Arm;

namespace Kudu.Services.Functions
{
    [ArmControllerConfiguration]
    public class FunctionController : ApiController
    {
        private readonly IFunctionManager _manager;
        private readonly ITraceFactory _traceFactory;

        public FunctionController(IFunctionManager manager, ITraceFactory traceFactory)
        {
            _manager = manager;
            _traceFactory = traceFactory;
        }

        [HttpPost]
        public async Task<HttpResponseMessage> SyncTriggers()
        {
            var tracer = _traceFactory.GetTracer();
            using (tracer.Step("FunctionController.SyncTriggers"))
            {
                try
                {
                    await _manager.SyncTriggers();

                    return Request.CreateResponse(HttpStatusCode.OK);
                }
                catch (Exception ex)
                {
                    tracer.TraceError(ex);

                    return ArmUtils.CreateErrorResponse(Request, HttpStatusCode.BadRequest, ex);
                }
            }
        }
    }
}