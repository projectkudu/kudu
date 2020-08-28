using System;
using System.Configuration;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Xml.Linq;
using Kudu.Client.Infrastructure;
using Kudu.Core.Infrastructure;

namespace Kudu.TestHarness
{
    public class KuduUtils
    {
        public static void DownloadDump(string serviceUrl, string zippedLogsPath, NetworkCredential credentials = null)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(zippedLogsPath));

                var clientHandler = HttpClientHelper.CreateClientHandler(serviceUrl, credentials);
                var client = new HttpClient(clientHandler);
                var result = client.GetAsync(serviceUrl + "api/dump").Result;
                if (result.IsSuccessStatusCode)
                {
                    using (Stream stream = result.Content.ReadAsStreamAsync().Result)
                    {
                        using (FileStream fs = File.Open(zippedLogsPath, FileMode.Create, FileAccess.Write))
                        {
                            stream.CopyTo(fs);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                TestTracer.Trace("Failed to download dump - {0}", ex.GetBaseException().Message);
            }
        }

        public static void KillKuduProcess(string serviceUrl, NetworkCredential credentials = null)
        {
            try
            {
                var clientHandler = HttpClientHelper.CreateClientHandler(serviceUrl, credentials);
                var client = new HttpClient(clientHandler);
                client.DeleteAsync(serviceUrl + "api/processes/0").Wait();
            }
            catch (Exception)
            {
                // no-op
            }
        }

        public static XDocument GetServerProfile(string serviceUrl, string logsTempPath, string appName, NetworkCredential credentials = null)
        {
            var zippedLogsPath = Path.Combine(logsTempPath, appName + ".zip");
            var unzippedLogsPath = Path.Combine(logsTempPath, appName);
            var profileLogPath = Path.Combine(unzippedLogsPath, "trace", "trace.xml");
            XDocument document = null;

            DownloadDump(serviceUrl, zippedLogsPath, credentials);

            if (File.Exists(zippedLogsPath))
            {
                ZipUtils.Unzip(zippedLogsPath, unzippedLogsPath);
                using (var stream = File.OpenRead(profileLogPath))
                {
                    document = XDocument.Load(stream);
                }
            }

            return document;
        }

        public static string SiteReusedForAllTests
        {
            get
            {
                string siteName = GetTestSetting("SiteReusedForAllTests");
                if (String.IsNullOrEmpty(siteName))
                {
                    return Environment.MachineName;
                }

                // Append the machine name to the site to avoid conflicting with other users running tests
                return String.Format("{0}{1}", siteName, Environment.MachineName);
            }
        }

        public static bool StopAfterFirstTestFailure
        {
            get
            {
                return GetBooleanTestSetting("StopAfterFirstTestFailure");
            }
        }
        
        public static string CustomKuduUrl
        {
            get
            {
                return GetTestSetting("CustomKuduUrl");
            }
        }

        public static bool RunningAgainstLinuxKudu
        {
            get
            {
                return GetBooleanTestSetting("RunningAgainstLinuxKudu");
            }
        }

        public static bool DisableRetry
        {
            get
            {
                return GetBooleanTestSetting("DisableRetry");
            }
        }

        public static bool GetBooleanTestSetting(string settingName)
        {
            bool retValue;

            if (bool.TryParse(GetTestSetting(settingName), out retValue))
            {
                return retValue;
            }

            return false;
        }

        public static string GetTestSetting(string settingName)
        {
            // If value exists as an environment setting use that otherwise try to get from app settings (for usage of the ci)
            string environmentValue = Environment.GetEnvironmentVariable(settingName);
            if (!String.IsNullOrEmpty(environmentValue))
            {
                return environmentValue;
            }
            else
            {
                return ConfigurationManager.AppSettings[settingName];
            }
        }

        public static IDisposable MockAzureEnvironment()
        {
            Environment.SetEnvironmentVariable("WEBSITE_INSTANCE_ID", "1234");
            return new DisposableAction(() => Environment.SetEnvironmentVariable("WEBSITE_INSTANCE_ID", null));
        }
    }
}
