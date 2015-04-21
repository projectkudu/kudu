using System;
using System.IO;

namespace Kudu.Core.Deployment.Generator
{
    public static class GoSiteEnabler
    {
        private const string GoFilePattern = "*.go";
        private const string MainPackage = "package main";

        /// <summary>
        /// <para>1. There are *.go files</para>
        /// <para>2. There is a main package</para>
        /// </summary>
        public static bool LooksLikeGo(string siteFolder)
        {
            string[] files = Directory.GetFiles(siteFolder, GoFilePattern, SearchOption.TopDirectoryOnly);
            foreach (var filePath in files)
            {
                string line = null;
                using (var reader = File.OpenText(filePath))
                {
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.StartsWith(MainPackage, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}
