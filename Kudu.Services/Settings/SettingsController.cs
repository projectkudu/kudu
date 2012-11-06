using System;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Kudu.Contracts.Settings;
using Newtonsoft.Json.Linq;

namespace Kudu.Services.Settings
{
    public class SettingsController : ApiController
    {
        private const string DeploymentSettingsSection = "deployment";
        private readonly IDeploymentSettingsManager _settingsManager;

        public SettingsController(IDeploymentSettingsManager settingsManager)
        {
            _settingsManager = settingsManager;
        }

        /// <summary>
        /// Create or change a setting
        /// </summary>
        /// <param name="pair">The name/value pair for the setting</param>
        /// <returns></returns>
        public HttpResponseMessage Set(JObject pair)
        {
            if (pair == null)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest);
            }

            string key = pair["key"].Value<string>();

            if (String.IsNullOrEmpty(key))
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest);
            }

            string value = pair["value"].Value<string>();

            _settingsManager.SetValue(key, value);

            return Request.CreateResponse(HttpStatusCode.NoContent);
        }

        /// <summary>
        /// Delete a setting
        /// </summary>
        /// <param name="key">The key of the setting to delete</param>
        /// <returns></returns>
        public HttpResponseMessage Delete(string key)
        {
            if (String.IsNullOrEmpty(key))
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest);
            }

            _settingsManager.DeleteValue(key);

            return Request.CreateResponse(HttpStatusCode.NoContent);
        }

        /// <summary>
        /// Get the list of all settings
        /// </summary>
        /// <returns></returns>
        public HttpResponseMessage GetAll()
        {
            var values = _settingsManager.GetValues();

            return Request.CreateResponse(HttpStatusCode.OK, values);
        }

        /// <summary>
        /// Get the value of a setting
        /// </summary>
        /// <param name="key">The setting's key</param>
        /// <returns></returns>
        public HttpResponseMessage Get(string key)
        {
            if (String.IsNullOrEmpty(key))
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest);
            }

            string value = _settingsManager.GetValue(key);

            if (value == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, String.Format(Resources.SettingDoesNotExist, key));
            }

            return Request.CreateResponse(HttpStatusCode.OK, value);
        }
    }
}
