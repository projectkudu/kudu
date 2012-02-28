using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Xml.Linq;
using Ionic.Zip;

namespace Kudu.TestHarness
{
    internal class KuduUtils
    {
        public static void DownloadDump (string serviceUrl, string zipfilePath)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(zipfilePath));

                var client = new HttpClient();
                var result = client.GetAsync(serviceUrl + "diag").Result;
                if (result.IsSuccessStatusCode)
                {
                    using (Stream stream = result.Content.ReadAsStreamAsync().Result)
                    {
                        using (FileStream fs = File.OpenWrite(zipfilePath))
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

        public static XDocument GetServerProfile(string serviceUrl, string zippedLogsPath, string appName = null)
        {                        
            var unzippedLogsPath = Path.Combine(Path.GetDirectoryName(zippedLogsPath), appName);
            var profileLogPath = Path.Combine(unzippedLogsPath, "profiles", "profile.xml");
            XDocument document = null;

            DownloadDump(serviceUrl, zippedLogsPath);                       

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
        
    }
}
