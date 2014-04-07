using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kudu.Contracts.Settings;
using Kudu.Contracts.SiteExtensions;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;
using NuGet;

namespace Kudu.Core.SiteExtensions
{
    public class SiteExtensionManager : ISiteExtensionManager
    {
        private readonly IPackageRepository _remoteRepository;
        private readonly IPackageRepository _localRepository;
        private readonly ITraceFactory _traceFactory;

        public SiteExtensionManager(IEnvironment environment, IDeploymentSettingsManager settings, ITraceFactory traceFactory)
        {
            _localRepository = new LocalPackageRepository(environment.RootPath + "\\SiteExtensions");
            _traceFactory = traceFactory;

            var remoteSource = new Uri(settings.GetSiteExtensionRemoteUrl());
            _remoteRepository = new DataServicePackageRepository(remoteSource);
        }

        public IEnumerable<SiteExtensionInfo> GetRemoteExtensions(string filter, bool allowPrereleaseVersions = false)
        {
            IQueryable<IPackage> packages;

            if (String.IsNullOrEmpty(filter))
            {
                packages = _remoteRepository.GetPackages()
                    .Where(p => p.IsLatestVersion)
                    .OrderByDescending(p => p.DownloadCount);
            }
            else
            {
                packages = _remoteRepository.Search(filter, allowPrereleaseVersions)
                    .Where(p => p.IsLatestVersion);
            }

            return packages.Select(ConvertRemotePackageToSiteExtensionInfo).AsEnumerable();
        }

        public SiteExtensionInfo GetRemoteExtension(string id, string version = null)
        {
            var semanticVersion = version == null ? null : new SemanticVersion(version);
            IPackage package = _remoteRepository.FindPackage(id, semanticVersion);
            if (package == null)
            {
                return null;
            }

            return ConvertRemotePackageToSiteExtensionInfo(package);
        }

        public IEnumerable<SiteExtensionInfo> GetLocalExtensions(string filter, bool checkLatest = true)
        {
            return _localRepository.Search(filter, false)
                .AsEnumerable()
                .Select(info => ConvertLocalPackageToSiteExtensionInfo(info, checkLatest));
        }

        public SiteExtensionInfo GetLocalExtension(string id, bool checkLatest = true)
        {
            IPackage package = _localRepository.FindPackage(id);
            if (package == null)
            {
                return null;
            }

            return ConvertLocalPackageToSiteExtensionInfo(package, checkLatest);
        }

        public SiteExtensionInfo InstallExtension(SiteExtensionInfo info)
        {
            return InstallExtension(info.Id);
        }

        public SiteExtensionInfo InstallExtension(string id)
        {
            IPackage package = _remoteRepository.FindPackage(id);
            if (package == null)
            {
                return null;
            }

            // Directory where _localRepository.AddPackage would use.
            string installationDirectory = GetInstallationDirectory(id);

            bool success = InstallExtension(package, installationDirectory);

            if (success)
            {
                return ConvertLocalPackageToSiteExtensionInfo(package);
            }

            return null;
        }

        public bool InstallExtension(IPackage package, string installationDirectory)
        {
            try
            {
                if (FileSystemHelpers.DirectoryExists(installationDirectory))
                {
                    FileSystemHelpers.DeleteDirectorySafe(installationDirectory);
                }

                foreach (IPackageFile file in package.GetContentFiles())
                {
                    // It is necessary to place applicationHost.xdt under site extension root.
                    string contentFilePath = file.Path.Substring("content/".Length);
                    string fullPath = Path.Combine(installationDirectory, contentFilePath);
                    FileSystemHelpers.CreateDirectory(Path.GetDirectoryName(fullPath));
                    using (Stream writeStream = FileSystemHelpers.OpenWrite(fullPath), readStream = file.GetStream())
                    {
                        OperationManager.Attempt(() => readStream.CopyTo(writeStream));
                    }
                }

                // If there is no xdt file, generate default.
                string xdtPath = Path.Combine(installationDirectory, "applicationHost.xdt");
                if (!FileSystemHelpers.FileExists(xdtPath))
                {
                    string xdtContent = CreateDefaultXdtFile(package.Id);
                    OperationManager.Attempt(() => FileSystemHelpers.WriteAllText(xdtPath, xdtContent));
                }

                // Copy nupkg file for package list/lookup
                FileSystemHelpers.CreateDirectory(installationDirectory);
                string packageFilePath = Path.Combine(installationDirectory,
                    String.Format("{0}.{1}.nupkg", package.Id, package.Version));
                using (
                    Stream readStream = package.GetStream(), writeStream = FileSystemHelpers.OpenWrite(packageFilePath))
                {
                    OperationManager.Attempt(() => readStream.CopyTo(writeStream));
                }
            }
            catch (Exception ex)
            {
                ITracer tracer = _traceFactory.GetTracer();
                tracer.TraceError(ex);
                FileSystemHelpers.DeleteDirectorySafe(installationDirectory);
                return false;
            }

            return true;
        }

        public bool UninstallExtension(string id)
        {
            string installationDirectory = GetInstallationDirectory(id);

            if (!FileSystemHelpers.DirectoryExists(installationDirectory))
            {
                throw new DirectoryNotFoundException(installationDirectory);
            }

            OperationManager.Attempt(() => FileSystemHelpers.DeleteDirectorySafe(installationDirectory));

            return !FileSystemHelpers.DirectoryExists(installationDirectory);
        }

        public string GetInstallationDirectory(string id)
        {
            return Path.Combine(_localRepository.Source, id);
        }

        private static string CreateDefaultXdtFile(string id)
        {
            return String.Format("<?xml version=\"1.0\" encoding=\"utf-8\"" + @"?>
<configuration " + "xmlns:xdt=\"http://schemas.microsoft.com/XML-Document-Transform\"" + @">
    <system.applicationHost>
        <sites>
            <site " + "name=\"%XDT_SCMSITENAME%\" xdt:Locator=\"Match(name)\"" + @" >
                <application " + "path=\"/{0}\" xdt:Locator=\"Match(path)\" xdt:Transform=\"Remove\"" + @"/>
                <application " + "path=\"/{0}\" applicationPool=\"%XDT_APPPOOLNAME%\" xdt:Transform=\"Insert\"" + @">
                    <virtualDirectory " + "path=\"/\" physicalPath=\"%XDT_EXTENSIONPATH%\"" + @"/>
                </application>
            </site>
        </sites>
    </system.applicationHost>
</configuration>", id);
        }

        public void UpdateLocalInfo(SiteExtensionInfo info)
        {
            string localPath = GetInstallationDirectory(info.Id);
            if (FileSystemHelpers.DirectoryExists(localPath))
            {
                info.SiteUrl = "/" + info.Id + "/";
                info.LocalPath = localPath;
                info.InstalledDateTime = FileSystemHelpers.GetLastWriteTimeUtc(info.LocalPath);
            }
        }

        public SiteExtensionInfo ConvertRemotePackageToSiteExtensionInfo(IPackage package)
        {
            var info = new SiteExtensionInfo(package);

            IPackage localPackage = _localRepository.FindPackage(info.Id);
            if (localPackage != null)
            {
                UpdateLocalInfo(info);
                // Assume input package (from remote) is always the latest version.
                info.LocalIsLatestVersion = package.Version == localPackage.Version;
            }

            return info;
        }

        public SiteExtensionInfo ConvertLocalPackageToSiteExtensionInfo(IPackage package, bool checkLatest = true)
        {
            var info = new SiteExtensionInfo(package);

            UpdateLocalInfo(info);
            if (checkLatest)
            {
                // FindPackage gets back the latest version.
                IPackage latestPackage = _remoteRepository.FindPackage(info.Id);
                info.LocalIsLatestVersion = package.Version == latestPackage.Version;
            }

            return info;
        }
    }
}
