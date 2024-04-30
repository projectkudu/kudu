using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Kudu.Core.Helpers
{
    public static class KeyVaultReferenceHelper
    {
        private const string AppSettingPrefix = "APPSETTING_";
        private const string KeyVaultReferenceInfoEnvVar = "WEBSITE_KEYVAULT_REFERENCES";

        /// Example dictionary:
        ///     {
        ///         "secret1":
        ///             {
        ///                 { "rawReference", "@Microsoft.KeyVault(SecretUri=)" },
        ///                 { "status", "ValueNotFound" }
        ///             }
        ///     }
        /// </summary>
        private static Dictionary<string, Dictionary<string, string>> KeyVaultReferencesInformation = GetKeyVaultReferencesInformation();

        public static int NumKeyVaultReferences
        {
            get
            {
                return KeyVaultReferencesInformation.Count();
            }
        }

        /// <summary>
        /// Simple filter to hide secrets from KeyVault references.
        /// </summary>
        /// <param name="environmentVariables">All variables for the site</param>
        /// <param name="hideKeyVaultSecrets">Whether to hide KeyVault secrets</param>
        /// <returns>Filtered environment variables</returns>
        public static IDictionary<object, object> KeyVaultReferencesFilter(IDictionary variables, bool hideKeyVaultSecrets)
        {
            IDictionary<object, object> filteredEnvironmentVariables = new Dictionary<object, object>();
            foreach (var entry in variables.Keys)
            {
                filteredEnvironmentVariables.Add(entry, HideKeyVaultSecret(entry, variables[entry], hideKeyVaultSecrets));
            }
            return filteredEnvironmentVariables;
        }

        /// <summary>
        /// Deserializes KeyVault References information in the form of a Dictionary<string, Dictionary<string, string>>
        /// </summary>
        /// <param name="serializedInformationBlob">Serialized dictionary containing KeyVault reference information</param>
        public static Dictionary<string, Dictionary<string, string>> GetKeyVaultReferencesInformation()
        {
            try
            {
                var serializedInformationBlob = System.Environment.GetEnvironmentVariable(KeyVaultReferenceInfoEnvVar);
                if (serializedInformationBlob != null)
                {
                    var result = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(serializedInformationBlob);
                    if (result != null)
                    {
                        return result;
                    }
                }
            }
            catch (Exception)
            {
            }

            return new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Hides a KeyVault secret if enabled, or just returns the original value if disabled
        /// </summary>
        /// <param name="key">Environment variable key</param>
        /// <param name="value">Environment variable original value</param>
        /// <param name="hideValue">Whether to hide</param>
        /// <returns>Hidden value (or original)</returns>
        public static object HideKeyVaultSecret(object key, object value, bool hideValue)
        {
            var keyString = (string) key;
            if (hideValue && KeyVaultReferencesInformation.ContainsKey(keyString))
            {
                try
                {
                    return "[Hidden - " + KeyVaultReferencesInformation[keyString]["status"] + ": " + KeyVaultReferencesInformation[keyString]["rawReference"] + "]";
                }
                catch (Exception)
                {
                    return "[Hidden KeyVault secret]";
                }
            }

            return value;
        }
    }
}
