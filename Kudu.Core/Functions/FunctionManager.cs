using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;
using Newtonsoft.Json.Linq;

namespace Kudu.Core.Functions
{
    public class FunctionManager : IFunctionManager
    {
        private const string HostJsonFile = "host.json";
        private const string FunctionJsonFile = "function.json";

        private readonly IEnvironment _environment;
        private readonly ITraceFactory _traceFactory;

        public FunctionManager(IEnvironment environment, ITraceFactory traceFactory)
        {
            _environment = environment;
            _traceFactory = traceFactory;
        }

        public async Task SyncTriggers()
        {
            var tracer = _traceFactory.GetTracer();
            using (tracer.Step("FunctionManager.SyncTriggers"))
            {
                if (!IsFunctionEnabled())
                {
                    tracer.Trace("This is not a function-enabled site!");
                    return; 
                }

                var inputs = GetTriggerInputs(tracer);
                if (inputs.Count == 0)
                {
                    tracer.Trace("No input triggers!");
                    return;
                }

                var client = new OperationClient(tracer);
                await client.PostAsync("/operations/settriggers", inputs);
            }
        }

        public bool IsFunctionEnabled()
        {
            // this should read appSettings instead
            var hostJson = Path.Combine(_environment.WebRootPath, HostJsonFile);
            return FileSystemHelpers.FileExists(hostJson);
        }

        public JArray GetTriggerInputs(ITracer tracer)
        {
            JArray inputs = new JArray();
            foreach (var functionJson in EnumerateFunctionFiles())
            {
                try
                {
                    var json = JObject.Parse(FileSystemHelpers.ReadAllText(functionJson));

                    JToken disabled;
                    if (json.TryGetValue("disabled", out disabled) && (bool)disabled)
                    {
                        tracer.Trace(String.Format("{0} is disabled", functionJson));
                        continue;
                    }

                    var binding = json.Value<JObject>("bindings");
                    foreach (JObject input in binding.Value<JArray>("input"))
                    {
                        var type = input.Value<string>("type");
                        if (type.EndsWith("Trigger", StringComparison.OrdinalIgnoreCase))
                        {
                            tracer.Trace(String.Format("Sync {0} of {1}", type, functionJson));
                            inputs.Add(input);
                        }
                        else
                        {
                            tracer.Trace(String.Format("Skip {0} of {1}", type, functionJson));
                        }
                    }
                }
                catch (Exception ex)
                {
                    tracer.Trace(String.Format("{0} is invalid. {1}", functionJson, ex.Message));
                }
            }

            return inputs;
        }

        public IEnumerable<string> EnumerateFunctionFiles()
        {
            foreach (var functionPath in FileSystemHelpers.GetDirectories(_environment.WebRootPath))
            {
                var functionJson = Path.Combine(functionPath, FunctionJsonFile);
                if (FileSystemHelpers.FileExists(functionJson))
                {
                    yield return functionJson;
                }
            }
        }
    }
}