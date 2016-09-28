using Newtonsoft.Json.Linq;
using System;

namespace Kudu.Core.Functions
{
    public interface IKeyJsonOps<T>
    {
        int NumberOfKeysInDefaultFormat
        {
            get;
        }
        
        // key generation is based on run time
        string GenerateKeyJson(Tuple<string,string>[] keyPairs, string functionRt, out string unencryptedKey);
        
        // read existing key file based on the content format, not the run time version
        string GetKeyValueFromJson(string json, out bool isEncrypted);

        T GenerateKeyObject(string functionKey, string functionName);
    }
}
