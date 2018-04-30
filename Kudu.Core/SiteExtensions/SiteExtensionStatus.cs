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
        private const string _siteExtensionType = "siteExtensionType";

        private string _filePath;
        private JsonSettings _jsonSettings;
        private ITracer _tracer;

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

        public SiteExtensionInfo.SiteExtensionType Type
        {
            get
            {
                string typeStr = SafeRead(_siteExtensionType);
                SiteExtensionInfo.SiteExtensionType type = SiteExtensionInfo.SiteExtensionType.Gallery;
                Enum.TryParse<SiteExtensionInfo.SiteExtensionType>(typeStr, out type);
                return type;
            }
            set { SafeWrite(_siteExtensionType, Enum.GetName(typeof(SiteExtensionInfo.SiteExtensionType), value)); }
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

        public SiteExtensionStatus(string rootPath, string id, ITracer tracer)
        {
            _filePath = GetFilePath(rootPath, id);
            _jsonSettings = new JsonSettings(_filePath);
            _tracer = tracer;
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

        /// <param name="siteExtensionRoot">should be $ROOT\SiteExtensions</param>
        public bool IsRestartRequired(string siteExtensionRoot)
        {
            return this.IsSiteExtensionRequireRestart(siteExtensionRoot);
        }

        /// <summary>
        /// <para>Scan every site extensions, check if there is any successful installation</para>
        /// <para>Looking for below cases:</para>
        /// <para>if not install to webroot, trigger restart; if install to webroot and with applicationHost.xdt file, trigger restart.</para>
        /// </summary>
        /// <param name="siteExtensionStatusRoot">should be $ROOT\site\siteextensions</param>
        /// <param name="siteExtensionRoot">should be $ROOT\SiteExtensions</param>
        public static bool IsAnyInstallationRequireRestart(string siteExtensionStatusRoot, string siteExtensionRoot, ITracer tracer, IAnalytics analytics)
        {
            try
            {
                using (tracer.Step("Checking if there is any installation require site restart ..."))
                {
                    string[] packageDirs = FileSystemHelpers.GetDirectories(siteExtensionStatusRoot);
                    // folder name is the package id
                    foreach (var dir in packageDirs)
                    {
                        try
                        {
                            DirectoryInfo dirInfo = new DirectoryInfo(dir);
                            var statusSettings = new SiteExtensionStatus(siteExtensionStatusRoot, dirInfo.Name, tracer);
                            if (statusSettings.IsSiteExtensionRequireRestart(siteExtensionRoot))
                            {
                                return true;
                            }
                        }
                        catch (Exception ex)
                        {
                            analytics.UnexpectedException(ex, trace: false);
                            tracer.TraceError(ex, "Failed to query {0} under {1}, continus to check others ...", _statusSettingsFileName, dir);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                analytics.UnexpectedException(ex, trace: false);
                tracer.TraceError(ex, "Not able to query directory under {0}", siteExtensionStatusRoot);
            }

            return false;
        }

        private bool IsSiteExtensionRequireRestart(string siteExtensionRoot)
        {
            if (Operation == Constants.SiteExtensionOperationInstall
                && ProvisioningState == Constants.SiteExtensionProvisioningStateSucceeded)
            {
                if (Type != SiteExtensionInfo.SiteExtensionType.WebRoot)
                {
                    // normal path
                    // if it is not installed to webroot and installation finish successfully, we should restart site
                    return true;
                }
                else
                {
                    // if it is intalled to webroot, restart site ONLY if there is an applicationHost.xdt/scmApplicationHost.xdt file under site extension folder
                    DirectoryInfo dirInfo = new DirectoryInfo(Path.GetDirectoryName(_filePath));
                    // folder name is the id of the package
                    string xdtFilePath = Path.Combine(siteExtensionRoot, dirInfo.Name, Constants.ApplicationHostXdtFileName);
                    // technically if it's "applicationHost.xdt", we should restart only the main site
                    // but for backwards compatability, if either one is detected, we tell the GEO master to restart the SCM SITE
                    string scmXdtFilePath = Path.Combine(siteExtensionRoot, dirInfo.Name, Constants.ScmApplicationHostXdtFileName);
                    return FileSystemHelpers.FileExists(xdtFilePath) || FileSystemHelpers.FileExists(scmXdtFilePath);
                }
            }

            return false;
        }

        private string SafeRead(string key)
        {
            try
            {
                return _jsonSettings.GetValue(key);
            }
            catch (Exception ex)
            {
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
