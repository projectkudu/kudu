using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Xml.Linq;
using Kudu.Client.Infrastructure;

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
                var result = client.GetAsync(serviceUrl + "dump").Result;
                if (result.IsSuccessStatusCode)
                {
                    using (Stream stream = result.Content.ReadAsStreamAsync().Result)
                    {
                        using (FileStream fs = File.OpenWrite(zippedLogsPath))
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

        public static string GetRandomWebsiteName(string name)
        {
            if (KuduUtils.ReuseSameSiteForAllTests)
            {
                return KuduUtils.SiteReusedForAllTests;
            }
            else
            {
                int maxLen = Math.Min(name.Length, 25);
                return name.Substring(0, maxLen) + Guid.NewGuid().ToString("N").Substring(0, 4);
            }
        }

        public static string SiteReusedForAllTests
        {
            get
            {
                string siteName = GetTestSetting("SiteReusedForAllTests");
                if (String.IsNullOrEmpty(siteName))
                {
                    return null;
                }

                // Append the machine name to the site to avoid conflicting with other users running tests
                return siteName + Environment.MachineName;
            }
        }

        public static bool ReuseSameSiteForAllTests
        {
            get
            {
                return !String.IsNullOrEmpty(SiteReusedForAllTests);
            }
        }

        public static bool TestOriginalSiteBuilderFactory
        {
            get
            {
                bool retValue;

                if (bool.TryParse(GetTestSetting("TestOriginalSiteBuilderFactory"), out retValue))
                {
                    return retValue;
                }

                return false;
            }
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
    }
}
