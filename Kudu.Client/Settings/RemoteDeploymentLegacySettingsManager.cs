using System;
using System.Collections.Specialized;
using System.Net;
using System.Threading.Tasks;
using Kudu.Client.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Kudu.Client.Deployment
{
    /// <summary>
    /// This class is used for testing the legacy settings API
    /// </summary>
    public class RemoteDeploymentLegacySettingsManager : RemoteDeploymentSettingsManager
    {
        public RemoteDeploymentLegacySettingsManager(string serviceUrl, ICredentials credentials = null)
            : base(serviceUrl, credentials)
        {
        }

        public new async Task<NameValueCollection> GetValues()
        {
            var obj = await Client.GetJsonAsync<JArray>(String.Empty);

            var nvc = new NameValueCollection();

            foreach (JObject value in obj)
            {
                try
                {
                    nvc[GetProperty(value, "key")] = GetProperty(value, "value");
                }
                catch (Exception e)
                {
                    // Include the payload in the exception for diagnostic
                    throw new Exception("Payload: " + obj.ToString(), e);
                }
            }

            return nvc;
        }
    }
}
