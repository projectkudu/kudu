using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Kudu.Core.SourceControl;
using Newtonsoft.Json.Linq;

namespace Kudu.Core.Functions
{
    public interface IFunctionManager
    {
        Task SyncTriggersAsync();
        Task<FunctionEnvelope> CreateOrUpdateAsync(string name, FunctionEnvelope functionEnvelope);
        Task<IEnumerable<FunctionEnvelope>> ListFunctionsConfigAsync();
        Task<FunctionEnvelope> GetFunctionConfigAsync(string name);
        Task<JObject> GetHostConfigAsync();
        Task<JObject> PutHostConfigAsync(JObject content);
        void DeleteFunction(string name);
    }
}
