using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using System;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Kudu.Core.Functions
{
    public class FunctionSecretsJsonOps : IKeyJsonOps<FunctionSecrets>
    {
        public int NumberOfKeysInDefaultFormat
        {
            get
            {
                return 1;
            }
        }

        // have the schema related info enclosed in this class
        public string GenerateKeyJson(Tuple<string, string>[] keyPair, string functionRt, out string unencryptedKey)
        {
            unencryptedKey = keyPair[0].Item1;
            if (string.CompareOrdinal(functionRt, Constants.FunctionKeyNewFormat) < 0)
            {
                return $"{{\"key\":\"{unencryptedKey}\"}}";
            }
            else
            {
                return $"{{\"keys\":[{{\"name\":\"default\",\"value\":\"{keyPair[0].Item2}\",\"encrypted\": true }}]}}";
            }
        }

        public string GetKeyValueFromJson(string json, out bool isEncrypted)
        {
            try
            {
                JsonArray hostJson = JsonNode.Parse(json)?.AsArray();
                if (hostJson["key"]?.GetValue<JsonElement>().ValueKind == JsonValueKind.String)
                {
                    isEncrypted = false;
                    return (string)hostJson["key"];
                }
                else if (hostJson["keys"]?.GetValue<JsonElement>().ValueKind == JsonValueKind.Array)
                {
                    JsonArray keys = (JsonArray)hostJson["keys"];
                    if (keys.Count >= 1)
                    {
                        JsonObject keyObject = (JsonObject)keys[0];
                        for (int i = 1; i < keys.Count; i++)
                        {
                            // start from the second
                            // if we can't find the key named default, return the 1st key found
                            if (string.Equals((string)keys[i]["name"], "default", StringComparison.Ordinal))
                            {
                                keyObject = (JsonObject)keys[i];
                                break;
                            }
                        }
                        isEncrypted = (bool)keyObject["encrypted"];
                        return (string)keyObject["value"];
                    }
                }
            }
            catch (JsonException)
            {
                // all parse issue ==> format exception
            }
            throw new FormatException($"Invalid secrets json: {json}");
        }

        public FunctionSecrets GenerateKeyObject(string functionKey, string functionName)
        {
            return new FunctionSecrets
            {
                Key = functionKey,
                TriggerUrl = string.Format(@"https://{0}/api/{1}?code={2}", Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME") ?? "localhost", functionName, functionKey)
            };
        }
    }
}
