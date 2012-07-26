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

        public HttpResponseMessage Delete(string key)
        {
            if (String.IsNullOrEmpty(key))
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest);
            }

            _settingsManager.DeleteValue(key);

            return Request.CreateResponse(HttpStatusCode.NoContent);
        }

        public HttpResponseMessage GetAll()
        {
            var values = _settingsManager.GetValues();

            return Request.CreateResponse(HttpStatusCode.OK, values);
        }

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
