using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Kudu.Core.Functions;
using Kudu.Core.Tracing;
using Kudu.Services.Arm;
using System.IO;
using Newtonsoft.Json.Linq;

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

        [HttpPut]
        public Task<HttpResponseMessage> CreateOrUpdate(string name)
        {
            return CreateOrUpdateHelper(name, Request.Content.ReadAsAsync<FunctionEnvelope>());
        }

        [HttpPut]
        public Task<HttpResponseMessage> CreateOrUpdateArm(string name, ArmEntry<FunctionEnvelope> armFunctionEnvelope)
        {
            return CreateOrUpdateHelper(name, Task.FromResult(armFunctionEnvelope.Properties));
        }

        private async Task<HttpResponseMessage> CreateOrUpdateHelper(string name, Task<FunctionEnvelope> functionEnvelopeBuilder)
        {
            var tracer = _traceFactory.GetTracer();
            using (tracer.Step($"FunctionsController.CreateOrUpdate({name})"))
            {
                try
                {
                    var functionEnvelope = await functionEnvelopeBuilder;
                    functionEnvelope = await _manager.CreateOrUpdateAsync(name, functionEnvelope);
                    return Request.CreateResponse(HttpStatusCode.Created, ArmUtils.AddEnvelopeOnArmRequest(functionEnvelope, Request));
                }
                catch (FileNotFoundException ex)
                {
                    tracer.TraceError(ex);
                    return Request.CreateResponse(HttpStatusCode.NotFound, ex.Message);
                }
                catch (InvalidOperationException ex)
                {
                    tracer.TraceError(ex);
                    return Request.CreateResponse(HttpStatusCode.Conflict, ex.Message);
                }
            }
        }

        [HttpGet]
        public async Task<HttpResponseMessage> List()
        {
            var tracer = _traceFactory.GetTracer();
            using (tracer.Step("FunctionsController.list()"))
            {
                return Request.CreateResponse(HttpStatusCode.OK, ArmUtils.AddEnvelopeOnArmRequest(await _manager.ListFunctionsConfigAsync(), Request));
            }
        }

        [HttpGet]
        public async Task<HttpResponseMessage> Get(string name)
        {
            var tracer = _traceFactory.GetTracer();
            using (tracer.Step($"FunctionsController.Get({name})"))
            {
                return Request.CreateResponse(HttpStatusCode.OK, ArmUtils.AddEnvelopeOnArmRequest(await _manager.GetFunctionConfigAsync(name), Request));
            }
        }

        [HttpDelete]
        public HttpResponseMessage Delete(string name)
        {
            var tracer = _traceFactory.GetTracer();
            using (tracer.Step($"FunctionsController.Delete({name})"))
            {
                try
                {
                    _manager.DeleteFunction(name);
                    return Request.CreateResponse(HttpStatusCode.NoContent);
                }
                catch (FileNotFoundException ex)
                {
                    tracer.TraceError(ex);
                    return Request.CreateResponse(HttpStatusCode.NotFound, ex.Message);
                }

            }
        }

        [HttpGet]
        public async Task<HttpResponseMessage> GetHostSettings()
        {
            var tracer = _traceFactory.GetTracer();
            using (tracer.Step("FunctionsController.GetHostSettings()"))
            {
                return Request.CreateResponse(HttpStatusCode.OK, await _manager.GetHostConfigAsync());
            }
        }

        [HttpPut]
        public async Task<HttpResponseMessage> PutHostSettings()
        {
            var tracer = _traceFactory.GetTracer();
            using (tracer.Step("FunctionsController.PutHostSettings()"))
            {
                return Request.CreateResponse(HttpStatusCode.Created, await _manager.PutHostConfigAsync(await Request.Content.ReadAsAsync<JObject>()));
            }
        }

        [HttpPost]
        public async Task<HttpResponseMessage> SyncTriggers()
        {
            var tracer = _traceFactory.GetTracer();
            using (tracer.Step("FunctionController.SyncTriggers"))
            {
                try
                {
                    await _manager.SyncTriggersAsync();

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