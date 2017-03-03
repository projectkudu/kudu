using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Kudu.Contracts.Functions;
using Kudu.Contracts.Tracing;
using Newtonsoft.Json.Linq;

namespace Kudu.Core.Functions
{
    public interface IFunctionManager
    {
        Task SyncTriggersAsync(ITracer tracer = null);
        Task<FunctionEnvelope> CreateOrUpdateAsync(string name, FunctionEnvelope functionEnvelope, Action setConfigChanged);
        Task<IEnumerable<FunctionEnvelope>> ListFunctionsConfigAsync(FunctionTestData packageLimit);
        Task<FunctionEnvelope> GetFunctionConfigAsync(string name, FunctionTestData packageLimit);
        Task<FunctionSecrets> GetFunctionSecretsAsync(string name);
        Task<MasterKey> GetMasterKeyAsync();
        Task<JObject> GetHostConfigAsync();
        Task<JObject> PutHostConfigAsync(JObject content);
        void DeleteFunction(string name, bool ignoreErrors);
    }
}
