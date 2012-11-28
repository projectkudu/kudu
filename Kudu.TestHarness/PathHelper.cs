using System;
using System.Configuration;
using System.IO;

namespace Kudu.TestHarness
{
    internal static class PathHelper
    {
        // Hard code the path to the services site (makes it easier to debug)
        internal static readonly string ServiceSitePath = ConfigurationManager.AppSettings["serviceSitePath"] ?? Path.GetFullPath(@"..\..\..\Kudu.Services.Web");
        internal static readonly string SitesPath = ConfigurationManager.AppSettings["sitesPath"] ?? Path.GetFullPath(Path.Combine(@"C:\Temp", "KuduApps"));

        // Test paths
        internal static readonly string TestsRootPath = Path.Combine(Directory.GetCurrentDirectory(), "Tests");
        internal static readonly string LocalRepositoriesDir = Path.GetFullPath(Path.Combine(TestsRootPath, "TestRepositories"));
        internal static readonly string TestResultsPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "TestResults"));

        internal static readonly string ZippedRepositoriesDir = "ZippedRepositories";
        internal static readonly string ZippedWebSitesPath = "ZippedWebSites";
        internal static readonly string GitDeploymentTestsFile = "GitDeploymentTests.csv";

        public static string GetPath(string targetPath)
        {
            var directory = new DirectoryInfo(Directory.GetCurrentDirectory());

            while (directory.Parent != null)
            {
                string configPath = Path.Combine(directory.FullName, targetPath);

                if (File.Exists(configPath))
                {
                    return configPath;
                }

                directory = directory.Parent;
            }

            throw new InvalidOperationException("Unable to find file " + targetPath);
        }
    }
}