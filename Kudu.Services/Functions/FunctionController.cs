using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Http;
using Kudu.Contracts.Functions;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Core.Functions;
using Kudu.Core.Helpers;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;
using Kudu.Services.Arm;
using Kudu.Services.Filters;
using Kudu.Services.Infrastructure;
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
        private static readonly Regex FunctionNameValidationRegex = new Regex(@"^[a-z][a-z0-9_\-]{0,127}$(?<!^host$)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

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
            if (!FunctionNameValidationRegex.IsMatch(name))
            {
                // it returns the same error object if the PUT request does not come from Arm
                return ArmUtils.CreateErrorResponse(Request, HttpStatusCode.BadRequest, new ArgumentException($"{name} is not a valid function name"));
            }

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
                var functions = (await _manager.ListFunctionsConfigAsync(ArmUtils.IsArmRequest(Request) ? new FunctionTestData() : null)).Select(f => AddFunctionAppIdToEnvelope(f));
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
                        AddFunctionAppIdToEnvelope(await _manager.GetFunctionConfigAsync(name, ArmUtils.IsArmRequest(Request) ? new FunctionTestData() : null)), Request));
            }
        }

        [HttpGet]
        public HttpResponseMessage GetAdminToken()
        {
            var tracer = _traceFactory.GetTracer();
            using (tracer.Step("FunctionsController.GetAdminToken()"))
            {
                return Request.CreateResponse(HttpStatusCode.OK, _manager.GetAdminToken());
            }
        }

        [HttpGet]
        public async Task<HttpResponseMessage> GetMasterKey()
        {
            var tracer = _traceFactory.GetTracer();
            using (tracer.Step("FunctionsController.GetMasterKey()"))
            {
                try
                {
                    return Request.CreateResponse(HttpStatusCode.OK, await _manager.GetMasterKeyAsync());
                }
                catch (InvalidOperationException ex)
                {
                    return Request.CreateErrorResponse(HttpStatusCode.Conflict, ex);
                }
            }
        }

        [HttpPost]
        public async Task<HttpResponseMessage> GetSecrets(string name)
        {
            // "name".json will be created as function keys, (runtime will always have lowercase "name")
            // kudu REST api does not care, "name" can be camelcase (ex: function portal)
            // windows file system is case insensitive, but this might not work in linux
            var tracer = _traceFactory.GetTracer();
            using (tracer.Step($"FunctionsController.GetSecrets({name})"))
            {
                try
                {
                    return Request.CreateResponse(HttpStatusCode.OK, await _manager.GetFunctionSecretsAsync(name));
                }
                catch (InvalidOperationException ex)
                {
                    return ArmUtils.CreateErrorResponse(Request, HttpStatusCode.Conflict, ex);
                }
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
                await PostDeploymentHelper.SyncFunctionsTriggers(_environment.RequestId, new PostDeploymentTraceListener(tracer));

                // Return a dummy body to make it valid in ARM template action evaluation
                return Request.CreateResponse(HttpStatusCode.OK, new { status = "success" });
            }
        }

        [HttpGet]
        public HttpResponseMessage DownloadFunctions(bool includeCsproj = true, bool includeAppSettings = false)
        {
            var tracer = _traceFactory.GetTracer();
            using (tracer.Step($"{nameof(FunctionController)}.{nameof(DownloadFunctions)}({includeCsproj}, {includeAppSettings})"))
            {
                var appName = ServerConfiguration.GetApplicationName();
                var fileName = $"{appName}.zip";
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = ZipStreamContent.Create(fileName, tracer, zip => _manager.CreateArchive(zip, includeAppSettings, includeCsproj, appName))
                };
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
                        await PostDeploymentHelper.SyncFunctionsTriggers(_environment.RequestId, new PostDeploymentTraceListener(bgTracer));
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