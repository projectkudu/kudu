using System;
using System.IO;
using System.Xml.Linq;
using Ionic.Zip;

namespace Kudu.Web.Infrastructure
{
    public static class ZipHelper
    {
        public static XDocument ExtractTrace(Stream zipStream)
        {
            using (var zip = ZipFile.Read(zipStream))
            {
                foreach (var entry in zip)
                {
                    if (entry.FileName.EndsWith("trace.xml", StringComparison.OrdinalIgnoreCase))
                    {
                        return XDocument.Load(entry.OpenReader());
                    }
                }
            }

            return null;
        }
    }
}