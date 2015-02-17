using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Kudu.Contracts.SiteExtensions;
using Kudu.Core.Infrastructure;
using Kudu.Core.Settings;

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

        public string ProvisioningState
        {
            get { return _jsonSettings.GetValue(_provisioningStateSetting); }
            set { _jsonSettings.SetValue(_provisioningStateSetting, value); }
        }

        public string Comment
        {
            get { return _jsonSettings.GetValue(_commentMessageSetting); }
            set
            {
                _jsonSettings.SetValue(_commentMessageSetting, value);
            }
        }

        public HttpStatusCode Status
        {
            get
            {
                string statusStr = _jsonSettings.GetValue(_statusSetting);
                HttpStatusCode statusCode = HttpStatusCode.OK;
                Enum.TryParse<HttpStatusCode>(statusStr, out statusCode);
                return statusCode;
            }
            set { _jsonSettings.SetValue(_statusSetting, Enum.GetName(typeof(HttpStatusCode), value)); }
        }

        /// <summary>
        /// <para>Property to indicate current operation</para>
        /// <para>Empty/null means there is no recent operation</para>
        /// </summary>
        public string Operation
        {
            get { return _jsonSettings.GetValue(_operationSetting); }
            set { _jsonSettings.SetValue(_operationSetting, value); }
        }

        public SiteExtensionStatus(string rootPath, string id)
        {
            _filePath = GetFilePath(rootPath, id);
            _jsonSettings = new JsonSettings(_filePath);
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

        public async Task RemoveArmSettings()
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
            catch
            {
                // no-op
            }
        }

        public bool IsTerminalStatus()
        {
            return string.Equals(Constants.SiteExtensionProvisioningStateSucceeded, ProvisioningState, StringComparison.OrdinalIgnoreCase)
                || string.Equals(Constants.SiteExtensionProvisioningStateFailed, ProvisioningState, StringComparison.OrdinalIgnoreCase)
                || string.Equals(Constants.SiteExtensionProvisioningStateCanceled, ProvisioningState, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetFilePath(string rootPath, string id)
        {
            return Path.Combine(rootPath, id, _statusSettingsFileName);
        }
    }
}
