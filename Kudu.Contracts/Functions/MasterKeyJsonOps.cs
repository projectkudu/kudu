using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace Kudu.Core.Functions
{
    public class MasterKeyJsonOps : IKeyJsonOps<MasterKey>
    {
        public int NumberOfKeysInDefaultFormat
        {
            get
            {
                return 2; // 1 masterkey, 1 functionkey in host.json
            }
        }

        public string GenerateKeyJson(Tuple<string, string>[] keyPair, string functionRt, out string unencryptedKey)
        {
            unencryptedKey = keyPair[0].Item1;
            if (string.CompareOrdinal(functionRt, Constants.FunctionKeyNewFormat) < 0)
            {
                return $"{{\"masterKey\":\"{unencryptedKey}\",\"functionKey\":\"{keyPair[1].Item1}\"}}";
            }
            else
            {
                return $"{{\"masterKey\":{{\"name\":\"master\",\"value\":\"{keyPair[0].Item2}\",\"encrypted\": true }},\"functionKeys\":[{{\"name\": \"default\",\"value\": \"{keyPair[1].Item2}\",\"encrypted\": true}}]}}";
            }
        }

        public string GetKeyValueFromJson(string json, out bool isEncrypted)
        {
            try
            {
                JObject hostJson = JObject.Parse(json);
                if (hostJson["masterKey"]?.Type == JTokenType.String && hostJson["functionKey"]?.Type == JTokenType.String)
                {
                    isEncrypted = false;
                    return hostJson.Value<string>("masterKey");
                }
                else if (hostJson["masterKey"]?.Type == JTokenType.Object && hostJson["functionKeys"]?.Type == JTokenType.Array)
                {
                    JObject keyObject = hostJson.Value<JObject>("masterKey");
                    isEncrypted = keyObject.Value<bool>("encrypted");
                    return keyObject.Value<string>("value");
                }
            }
            catch (JsonException)
            {
                // all parse issue ==> format exception
            }
            throw new FormatException($"Invalid secrets json: {json}");
        }

        public MasterKey GenerateKeyObject(string masterKey, string Name)
        {
            // name is not used
            return new MasterKey { Key = masterKey };
        }
    }
}
