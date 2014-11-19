using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.Settings;
using Newtonsoft.Json.Linq;

namespace Kudu.Services.Settings
{
    public class SettingsController : ApiController
    {
        private const string DeploymentSettingsSection = "deployment";
        private readonly IDeploymentSettingsManager _settingsManager;
        private readonly IOperationLock _deploymentLock;

        public SettingsController(IDeploymentSettingsManager settingsManager, IOperationLock deploymentLock)
        {
            _settingsManager = settingsManager;
            _deploymentLock = deploymentLock;
        }

        /// <summary>
        /// Create or change some settings
        /// </summary>
        /// <param name="newSettings">The object containing the new settings</param>
        /// <returns></returns>
        public HttpResponseMessage Set(JObject newSettings)
        {
            if (newSettings == null)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest);
            }

            // We support two formats here:
            // 1. For backward compat, we support {key: 'someKey', value: 'someValue' }
            // 2. The preferred format is { someKey = 'someValue' }
            // Note that #2 allows multiple settings to be set, e.g. { someKey = 'someValue', someKey2 = 'someValue2' }

            try
            {
                return _deploymentLock.LockOperation(() =>
                {
                    JToken keyToken, valueToken;
                    if (newSettings.Count == 2 && newSettings.TryGetValue("key", out keyToken) && newSettings.TryGetValue("value", out valueToken))
                    {
                        string key = keyToken.Value<string>();

                        if (String.IsNullOrEmpty(key))
                        {
                            return Request.CreateResponse(HttpStatusCode.BadRequest);
                        }

                        _settingsManager.SetValue(key, valueToken.Value<string>());
                    }
                    else
                    {
                        foreach (var keyValuePair in newSettings)
                        {
                            _settingsManager.SetValue(keyValuePair.Key, keyValuePair.Value.Value<string>());
                        }
                    }

                    return Request.CreateResponse(HttpStatusCode.NoContent);
                }, TimeSpan.Zero);
            }
            catch (LockOperationException ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.Conflict, ex.Message);
            }
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

            try
            {
                return _deploymentLock.LockOperation(() =>
                {
                    _settingsManager.DeleteValue(key);

                    return Request.CreateResponse(HttpStatusCode.NoContent);
                }, TimeSpan.Zero);
            }
            catch (LockOperationException ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.Conflict, ex.Message);
            }
        }

        /// <summary>
        /// Get the list of all settings
        /// </summary>
        /// <returns></returns>
        public HttpResponseMessage GetAll()
        {
            return Request.CreateResponse(HttpStatusCode.OK, _settingsManager.GetValues());
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
