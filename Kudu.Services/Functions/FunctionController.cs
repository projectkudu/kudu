using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Kudu.Core.Functions;
using Kudu.Core.Tracing;
using Kudu.Services.Arm;
using Kudu.Services.Filters;
using Newtonsoft.Json.Linq;

namespace Kudu.Services.Functions
{
    [ArmControllerConfiguration]
    [FunctionExceptionFilter]
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
                var functionEnvelope = await functionEnvelopeBuilder;
                bool configChanged = false;
                functionEnvelope = await _manager.CreateOrUpdateAsync(name, functionEnvelope, () => { configChanged = true; });
                AddFunctionAppIdToEnvelope(functionEnvelope);

                // Don't await this call since we don't want slow down the operation. Sync can happen later
#pragma warning disable 4014
                if (configChanged)
                {
                    _manager.SyncTriggersAsync();
                }
#pragma warning restore 4014

                return Request.CreateResponse(HttpStatusCode.Created, ArmUtils.AddEnvelopeOnArmRequest(functionEnvelope, Request));
            }
        }

        [HttpGet]
        public async Task<HttpResponseMessage> List()
        {
            var tracer = _traceFactory.GetTracer();
            using (tracer.Step("FunctionsController.list()"))
            {
                var functions = (await _manager.ListFunctionsConfigAsync()).Select(f => AddFunctionAppIdToEnvelope(f));
                return Request.CreateResponse(HttpStatusCode.OK, ArmUtils.AddEnvelopeOnArmRequest(functions, Request));
            }
        }

        [HttpGet]
        public async Task<HttpResponseMessage> Get(string name)
        {
            var tracer = _traceFactory.GetTracer();
            using (tracer.Step($"FunctionsController.Get({name})"))
            {
                return Request.CreateResponse(HttpStatusCode.OK,
                    ArmUtils.AddEnvelopeOnArmRequest(
                        AddFunctionAppIdToEnvelope(await _manager.GetFunctionConfigAsync(name)), Request));
            }
        }

        [HttpDelete]
        public HttpResponseMessage Delete(string name)
        {
            var tracer = _traceFactory.GetTracer();
            using (tracer.Step($"FunctionsController.Delete({name})"))
            {
                _manager.DeleteFunction(name);

                // Don't await this call since we don't want slow down the operation. Sync can happen later
                _manager.SyncTriggersAsync();

                return Request.CreateResponse(HttpStatusCode.NoContent);
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
                await _manager.SyncTriggersAsync();
                return Request.CreateResponse(HttpStatusCode.OK);
            }
        }

        // Compute the site ID, for both the top level function API case and the regular nested case
        private FunctionEnvelope AddFunctionAppIdToEnvelope(FunctionEnvelope function)
        {
            Uri referrer = Request.Headers.Referrer;
            if (referrer == null) return function;

            string armId = referrer.AbsolutePath;

            const string msWeb = "Microsoft.Web";
            const string functionResource = msWeb + "/functions";
            const string sitesResource = msWeb + "/sites";

            // Input: /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.Web/functions/{funcname}
            int index = armId.IndexOf(functionResource, StringComparison.OrdinalIgnoreCase);
            if (index > 0)
            {
                // Produce: /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.Web/sites/{sitename}
                function.FunctionAppId = $"{armId.Substring(0, index)}{sitesResource}/{Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME")}";
                return function;
            }

            // Input: /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.Web/sites/{sitename}/functions/{funcname}
            index = armId.IndexOf(sitesResource, StringComparison.OrdinalIgnoreCase);
            if (index > 0)
            {
                // Produce: /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.Web/sites/{sitename}
                index = armId.IndexOf("/", index + sitesResource.Length + 1, StringComparison.OrdinalIgnoreCase);
                function.FunctionAppId = armId.Substring(0, index);
                return function;
            }

            return function;
        }
    }
}