using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Kudu.Core.SourceControl;
using Kudu.Contracts.Jobs;
using Newtonsoft.Json.Linq;

namespace Kudu.Core.Functions
{
    public interface IFunctionManager
    {
        Task SyncTriggers();
        Task<FunctionEnvelope> CreateOrUpdate(string name, FunctionEnvelope functionEnvelope);
        Task<IEnumerable<FunctionEnvelope>> ListFunctionsConfig();
        Task<FunctionEnvelope> GetFunctionConfig(string name);
        Task<JObject> GetHostConfig();
        Task<JObject> PutHostConfig(JObject content);
        IEnumerable<FunctionTemplate> GetTemplates();
        void DeleteFunction(string name);
    }
}
