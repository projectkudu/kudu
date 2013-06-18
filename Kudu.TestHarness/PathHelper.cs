using System;
using System.IO;

namespace Kudu.TestHarness
{
    internal static class PathHelper
    {
        private static readonly string _tempPath = Path.Combine(Path.GetPathRoot(Directory.GetCurrentDirectory()), "Kudu-Test-Files");
        private static readonly string _localRepositoriesDir = Path.Combine(_tempPath, "TestRepositories", Path.GetRandomFileName());
        private static readonly string _repositoryCachePath = KuduUtils.GetTestSetting("repositoryCachePath") ?? Path.Combine(_tempPath, "RepositoryCache");
        private static readonly string _testLockPath = KuduUtils.GetTestSetting("testLockPath") ?? Path.Combine(_tempPath, "TestLocks");

        // Hard code the path to the services site (makes it easier to debug)
        internal static readonly string ServiceSitePath = KuduUtils.GetTestSetting("serviceSitePath") ?? Path.GetFullPath(@"..\..\..\Kudu.Services.Web");
        internal static readonly string SitesPath = KuduUtils.GetTestSetting("sitesPath") ?? Path.Combine(_tempPath, "KuduApps");

        // Test paths
        internal static readonly string TestsRootPath = Path.Combine(Directory.GetCurrentDirectory(), "Tests");

        internal static readonly string TestResultsPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "TestResults"));

        internal static readonly string ZippedRepositoriesDir = "ZippedRepositories";
        internal static readonly string ZippedWebSitesPath = "ZippedWebSites";

        internal static string LocalRepositoriesDir
        {
            get
            {
                EnsureDirectory(_localRepositoriesDir);
                return _localRepositoriesDir;
            }
        }

        internal static string RepositoryCachePath
        {
            get
            {
                EnsureDirectory(_repositoryCachePath);
                return _repositoryCachePath;
            }
        }

        internal static string TestLockPath
        {
            get
            {
                EnsureDirectory(_testLockPath);
                return _testLockPath;
            }
        }

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

        public static void EnsureDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }
    }
}