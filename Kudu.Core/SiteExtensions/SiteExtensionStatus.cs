using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Kudu.Contracts.SiteExtensions;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;
using Kudu.Core.Settings;
using Kudu.Core.Tracing;
using Newtonsoft.Json.Linq;

namespace Kudu.Core.SiteExtensions
{
    public class SiteExtensionStatus
    {
        private const string _statusSettingsFileName = "SiteExtensionStatus.json";
        private const string _provisioningStateSetting = "provisioningState";
        private const string _commentMessageSetting = "comment";
        private const string _statusSetting = "status";
        private const string _operationSetting = "operation";

        private string _filePath;
        private JsonSettings _jsonSettings;
        private ITracer _tracer;
        private IAnalytics _analytics;

        public string ProvisioningState
        {
            get { return SafeRead(_provisioningStateSetting); }
            set { SafeWrite(_provisioningStateSetting, value); }
        }

        public string Comment
        {
            get { return SafeRead(_commentMessageSetting); }
            set { SafeWrite(_commentMessageSetting, value); }
        }

        public HttpStatusCode Status
        {
            get
            {
                string statusStr = SafeRead(_statusSetting);
                HttpStatusCode statusCode = HttpStatusCode.OK;
                Enum.TryParse<HttpStatusCode>(statusStr, out statusCode);
                return statusCode;
            }
            set { SafeWrite(_statusSetting, Enum.GetName(typeof(HttpStatusCode), value)); }
        }

        /// <summary>
        /// <para>Property to indicate current operation</para>
        /// <para>Empty/null means there is no recent operation</para>
        /// </summary>
        public string Operation
        {
            get { return _jsonSettings.GetValue(_operationSetting); }
            set { SafeWrite(_operationSetting, value); }
        }

        public SiteExtensionStatus(string rootPath, string id, ITracer tracer, IAnalytics analytics)
        {
            _filePath = GetFilePath(rootPath, id);
            _jsonSettings = new JsonSettings(_filePath);
            _tracer = tracer;
            _analytics = analytics;
        }

        public void FillSiteExtensionInfo(SiteExtensionInfo info, string defaultProvisionState = null)
        {
            info.ProvisioningState = ProvisioningState ?? defaultProvisionState;
            info.Comment = Comment;
        }

        public void ReadSiteExtensionInfo(SiteExtensionInfo info)
        {
            ProvisioningState = info.ProvisioningState;
            Comment = info.Comment;
        }

        public async Task RemoveStatus()
        {
            try
            {
                string dirName = Path.GetDirectoryName(_filePath);
                if (FileSystemHelpers.DirectoryExists(dirName))
                {
                    FileSystemHelpers.DeleteDirectoryContentsSafe(dirName);
                    // call DeleteDirectorySafe directly would sometime causing "Access denied" on folder
                    // work-around: remove content and wait briefly before delete folder 
                    await Task.Delay(300);
                    FileSystemHelpers.DeleteDirectorySafe(dirName);
                }
            }
            catch (Exception ex)
            {
                _analytics.UnexpectedException(ex, trace: false);

                // no-op
                _tracer.TraceError(ex);
            }
        }

        public bool IsTerminalStatus()
        {
            return string.Equals(Constants.SiteExtensionProvisioningStateSucceeded, ProvisioningState, StringComparison.OrdinalIgnoreCase)
                || string.Equals(Constants.SiteExtensionProvisioningStateFailed, ProvisioningState, StringComparison.OrdinalIgnoreCase)
                || string.Equals(Constants.SiteExtensionProvisioningStateCanceled, ProvisioningState, StringComparison.OrdinalIgnoreCase);
        }

        private string SafeRead(string key)
        {
            try
            {
                return _jsonSettings.GetValue(key);
            }
            catch (Exception ex)
            {
                _analytics.UnexpectedException(ex, trace: false);

                _tracer.TraceError(ex);
                // if setting file happen to be invalid, e.g w3wp.exe was kill while writting
                // treat it as failed, and suggest user to re-install or un-install
                JObject newSettings = new JObject();
                newSettings[_provisioningStateSetting] = Constants.SiteExtensionProvisioningStateFailed;
                newSettings[_commentMessageSetting] = "Corrupted site extension, please re-install or uninstall extension.";
                newSettings[_statusSetting] = Enum.GetName(typeof(HttpStatusCode), HttpStatusCode.BadRequest);
                _jsonSettings.Save(newSettings);
            }

            return _jsonSettings.GetValue(key);
        }

        private void SafeWrite(string key, JToken value)
        {
            try
            {
                _jsonSettings.SetValue(key, value);
            }
            catch (Exception ex)
            {
                _analytics.UnexpectedException(ex, trace: false);

                _tracer.TraceError(ex);
                // if setting file happen to be invalid, e.g w3wp.exe was kill while writting
                // clear all content, start from blank
                JObject newSettings = new JObject();
                newSettings[key] = value;
                _jsonSettings.Save(newSettings);
            }
        }

        private static string GetFilePath(string rootPath, string id)
        {
            return Path.Combine(rootPath, id, _statusSettingsFileName);
        }
    }
}
