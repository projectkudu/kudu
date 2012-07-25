using System;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Newtonsoft.Json.Linq;
using XmlSettings;

namespace Kudu.Services.Settings
{
    public class SettingsController : ApiController
    {
        private const string DeploymentSettingsSection = "deployment";
        private readonly ISettings _settings;

        public SettingsController(ISettings settings)
        {
            _settings = settings;
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

            _settings.SetValue(DeploymentSettingsSection, key, value);

            return Request.CreateResponse(HttpStatusCode.NoContent);
        }

        public HttpResponseMessage Delete(string key)
        {
            if (String.IsNullOrEmpty(key))
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest);
            }

            _settings.DeleteValue(DeploymentSettingsSection, key);

            return Request.CreateResponse(HttpStatusCode.NoContent);
        }

        public HttpResponseMessage GetAll()
        {
            var values = _settings.GetValues(DeploymentSettingsSection);

            if (values == null)
            {
                return Request.CreateResponse(HttpStatusCode.OK, new string[0]);
            }

            return Request.CreateResponse(HttpStatusCode.OK, values);
        }

        public HttpResponseMessage Get(string key)
        {
            if (String.IsNullOrEmpty(key))
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest);
            }

            string value = _settings.GetValue(DeploymentSettingsSection, key);

            if (value == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, String.Format(Resources.SettingDoesNotExist, key));
            }

            return Request.CreateResponse(HttpStatusCode.OK, value);
        }
    }
}
