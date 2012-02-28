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
    internal class ZipUtils
    {
        public static void Unzip(string zipPath, string targetDir)
        {
            using (ZipFile zip = ZipFile.Read(zipPath))
            {
                foreach (ZipEntry file in zip)
                {
                    file.Extract(targetDir, ExtractExistingFileAction.OverwriteSilently);
                }
            }
        }       
    }
}
