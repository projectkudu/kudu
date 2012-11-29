using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Xml.Linq;

namespace Kudu.TestHarness
{
    public class KuduUtils
    {
        public static void DownloadDump(string serviceUrl, string zippedLogsPath, NetworkCredential credentials = null)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(zippedLogsPath));

                var client = new HttpClient(new HttpClientHandler()
                {
                    Credentials = credentials
                });


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
                Debug.WriteLine("Failed to download dump");
                Debug.WriteLine(ex.GetBaseException().Message);
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
                return ConfigurationManager.AppSettings["SiteReusedForAllTests"];
            }
        }

        public static bool ReuseSameSiteForAllTests
        {
            get
            {
                return !String.IsNullOrEmpty(SiteReusedForAllTests);
            }
        }
    }
}
