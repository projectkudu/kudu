using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Core.Functions;
using Kudu.Core.Tracing;
using Kudu.Services.Arm;
using Kudu.Services.Filters;
using Newtonsoft.Json.Linq;
using Environment = System.Environment;

namespace Kudu.Services.Functions
{
    [ArmControllerConfiguration]
    [FunctionExceptionFilter]
    public class FunctionController : ApiController
    {
        private readonly IFunctionManager _manager;
        private readonly ITraceFactory _traceFactory;
        private readonly IEnvironment _environment;

        public FunctionController(IFunctionManager manager, ITraceFactory traceFactory, IEnvironment environment)
        {
            _manager = manager;
            _traceFactory = traceFactory;
            _environment = environment;
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

                if (configChanged)
                {
                    // Fire and forget SyncTrigger request.
                    FireSyncTriggers(tracer);
                }

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
        
        [HttpGet]
        public async Task<HttpResponseMessage> GetMasterKey()
        {
            var tracer = _traceFactory.GetTracer();
            using (tracer.Step("FunctionsController.GetMasterKey()"))
            {
                return Request.CreateResponse(HttpStatusCode.OK, await _manager.GetMasterKeyAsync());
            }
        }

        [HttpPost]
        public async Task<HttpResponseMessage> GetSecrets(string name)
        {
            var tracer = _traceFactory.GetTracer();
            using (tracer.Step($"FunctionsController.GetSecrets({name})"))
            {
                return Request.CreateResponse(HttpStatusCode.OK, await _manager.GetFunctionSecretsAsync(name));
            }
        }

        [HttpDelete]
        public HttpResponseMessage Delete(string name)
        {
            var tracer = _traceFactory.GetTracer();
            using (tracer.Step($"FunctionsController.Delete({name})"))
            {
                _manager.DeleteFunction(name, ignoreErrors: false);

                // Fire and forget SyncTrigger request.
                FireSyncTriggers(tracer);

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

        private void FireSyncTriggers(ITracer tracer)
        {
            tracer.Trace("FunctionController.FireSyncTriggers");

            // create background tracer independent of request lifetime
            var bgTracer = new XmlTracer(_environment.TracePath, tracer.TraceLevel);

            // start new task to detach from request sync context
            Task.Run(async () =>
            {
                using (bgTracer.Step(XmlTracer.BackgroundTrace, new Dictionary<string, string>
                {
                    { "url", "/api/functions/synctriggers" },
                    { "method", "POST" }
                }))
                {
                    try
                    {
                        await _manager.SyncTriggersAsync(bgTracer);
                    }
                    catch (Exception ex)
                    {
                        bgTracer.TraceError(ex);
                    }
                }
            });
        }
    }
}