using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web;
using System.Xml;
using Kudu.Contracts.Settings;
using Kudu.Contracts.SiteExtensions;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;
using Newtonsoft.Json;
using NuGet;

namespace Kudu.Core.SiteExtensions
{
    public class SiteExtensionManager : ISiteExtensionManager
    {
        private readonly IPackageRepository _remoteRepository;
        private readonly IPackageRepository _localRepository;
        private readonly ITraceFactory _traceFactory;
        private const string _applicationHostFile = "applicationHost.xdt";
        private readonly string _baseUrl;
        private static readonly Dictionary<string, SiteExtensionInfo> _preInstalledExtensionDictionary
            = new Dictionary<string, SiteExtensionInfo>(StringComparer.OrdinalIgnoreCase)
        {
            {
                "monaco",
                new SiteExtensionInfo
                {
                    Id = "monaco",
                    Title = "Visual Studio Online \"Monaco\"",
                    Authors = new [] {"Chris Dias"},
                    LicenseUrl = "http://azure.microsoft.com/en-us/support/legal/",
                    ProjectUrl = "http://blogs.msdn.com/b/monaco/",
                    Description = "A full featured browser based development environment for editing your website",
                    // API will return a full url instead of this relative url.
                    ExtensionUrl = "/dev"
                }
            }
        };
        
        public SiteExtensionManager(IEnvironment environment, IDeploymentSettingsManager settings, ITraceFactory traceFactory, HttpContextBase context)
        {
            _localRepository = new LocalPackageRepository(environment.RootPath + "\\SiteExtensions");
            _traceFactory = traceFactory;

            var remoteSource = new Uri(settings.GetSiteExtensionRemoteUrl());
            _remoteRepository = new DataServicePackageRepository(remoteSource);

            _baseUrl = context.Request.Url == null ? "" : context.Request.Url.GetLeftPart(UriPartial.Authority).TrimEnd('/');
        }

        public IEnumerable<SiteExtensionInfo> GetRemoteExtensions(string filter, bool allowPrereleaseVersions = false)
        {
            IEnumerable<SiteExtensionInfo> preInstalledExtensions = GetPreInstalledExtensions(filter, showEnabledOnly: false);

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

            IEnumerable<SiteExtensionInfo> feedInfo = packages.Select(ConvertRemotePackageToSiteExtensionInfo);

            return preInstalledExtensions.Concat(feedInfo);
        }

        public SiteExtensionInfo GetRemoteExtension(string id, string version = null)
        {
            SiteExtensionInfo info = GetPreInstalledExtension(id);
            if (info != null)
            {
                return info;
            }

            var semanticVersion = version == null ? null : new NuGet.SemanticVersion(version);

            IPackage package = _remoteRepository.FindPackage(id, semanticVersion);
            if (package == null)
            {
                return null;
            }

            info = ConvertRemotePackageToSiteExtensionInfo(package);

            return info;
        }

        private IEnumerable<SiteExtensionInfo> GetPreInstalledExtensions(string filter, bool showEnabledOnly)
        {
            var list = new List<SiteExtensionInfo>();

            foreach (SiteExtensionInfo info in _preInstalledExtensionDictionary.Values)
            {
                if (String.IsNullOrEmpty(filter) ||
                    JsonConvert.SerializeObject(info).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var extension = GetPreInstalledExtension(info.Id);

                    if (!showEnabledOnly || extension.ExtensionUrl != null)
                    {
                        list.Add(extension);
                    }
                }
            }

            return list;
        }

        private SiteExtensionInfo GetPreInstalledExtension(string id)
        {
            if (_preInstalledExtensionDictionary.ContainsKey(id))
            {
                var info = new SiteExtensionInfo(_preInstalledExtensionDictionary[id]);
                SetLocalInfo(info);
                info.Version = GetLatestPreInstalledExtensionVersion(info.Id);

                return info;
            }
            else
            {
                return null;
            }
        }

        public IEnumerable<SiteExtensionInfo> GetLocalExtensions(string filter, bool checkLatest = true)
        {
            IEnumerable<SiteExtensionInfo> preInstalledExtensions = GetPreInstalledExtensions(filter, showEnabledOnly: true);

            IEnumerable<SiteExtensionInfo> feedInfo = _localRepository.Search(filter, false)
                .Select(info => ConvertLocalPackageToSiteExtensionInfo(info, checkLatest));

            return preInstalledExtensions.Concat(feedInfo);
        }

        public SiteExtensionInfo GetLocalExtension(string id, bool checkLatest = true)
        {
            SiteExtensionInfo info = GetPreInstalledExtension(id);
            if (info != null && info.ExtensionUrl != null)
            {
                return info;
            }

            IPackage package = _localRepository.FindPackage(id);
            if (package == null)
            {
                return null;
            }

            return ConvertLocalPackageToSiteExtensionInfo(package, checkLatest);
        }

        public SiteExtensionInfo InstallExtension(string id, string version)
        {
            if (_preInstalledExtensionDictionary.ContainsKey(id))
            {
                return EnablePreInstalledExtension(id);
            }
            else
            {
                IPackage localPackage = null;
                IPackage repoPackage = version == null ? _remoteRepository.FindPackage(id) :
                    _remoteRepository.FindPackage(id, new NuGet.SemanticVersion(version), allowPrereleaseVersions: true, allowUnlisted: true);

                if (repoPackage != null)
                {
                    // Directory where _localRepository.AddPackage would use.
                    string installationDirectory = GetInstallationDirectory(id);

                    localPackage = InstallExtension(repoPackage, installationDirectory);
                }

                return localPackage == null ? null : ConvertLocalPackageToSiteExtensionInfo(localPackage);
            }
        }

        private IPackage InstallExtension(IPackage package, string installationDirectory)
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
                GenerateApplicationHostXdt(installationDirectory, '/' + package.Id, isPreInstalled: false);

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
                return null;
            }

            return _localRepository.FindPackage(package.Id);
        }

        private SiteExtensionInfo EnablePreInstalledExtension(string id)
        {
            string installationDirectory = GetInstallationDirectory(id);

            try
            {
                if (FileSystemHelpers.DirectoryExists(installationDirectory))
                {
                    FileSystemHelpers.DeleteDirectorySafe(installationDirectory);
                }

                GenerateApplicationHostXdt(installationDirectory, _preInstalledExtensionDictionary[id].ExtensionUrl, isPreInstalled: true);
            }
            catch (Exception ex)
            {
                ITracer tracer = _traceFactory.GetTracer();
                tracer.TraceError(ex);
                FileSystemHelpers.DeleteDirectorySafe(installationDirectory);
                return null;
            }

            return GetPreInstalledExtension(id);
        }

        private static void GenerateApplicationHostXdt(string installationDirectory, string relativeUrl, bool isPreInstalled)
        {
            // If there is no xdt file, generate default.
            FileSystemHelpers.CreateDirectory(installationDirectory);
            string xdtPath = Path.Combine(installationDirectory, _applicationHostFile);
            if (!FileSystemHelpers.FileExists(xdtPath))
            {
                string xdtContent = CreateDefaultXdtFile(relativeUrl, isPreInstalled);
                OperationManager.Attempt(() => FileSystemHelpers.WriteAllText(xdtPath, xdtContent));
            }
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

        private string GetInstallationDirectory(string id)
        {
            return Path.Combine(_localRepository.Source, id);
        }

        private static string CreateDefaultXdtFile(string relativeUrl, bool isPreInstalled)
        {
            string physicalPath = isPreInstalled ? "%XDT_LATEST_EXTENSIONPATH%" : "%XDT_EXTENSIONPATH%";
            return String.Format("<?xml version=\"1.0\" encoding=\"utf-8\"" + @"?>
<configuration " + "xmlns:xdt=\"http://schemas.microsoft.com/XML-Document-Transform\"" + @">
    <system.applicationHost>
        <sites>
            <site " + "name=\"%XDT_SCMSITENAME%\" xdt:Locator=\"Match(name)\"" + @" >
                <application " + "path=\"{0}\" xdt:Locator=\"Match(path)\" xdt:Transform=\"Remove\"" + @"/>
                <application " + "path=\"{0}\" applicationPool=\"%XDT_APPPOOLNAME%\" xdt:Transform=\"Insert\"" + @">
                    <virtualDirectory " + "path=\"/\" physicalPath=\"{1}\"" + @"/>
                </application>
            </site>
        </sites>
    </system.applicationHost>
</configuration>", relativeUrl, physicalPath);
        }

        private void SetLocalInfo(SiteExtensionInfo info)
        {
            string localPath = GetInstallationDirectory(info.Id);
            if (FileSystemHelpers.DirectoryExists(localPath))
            {
                info.LocalPath = localPath;
                info.InstalledDateTime = FileSystemHelpers.GetLastWriteTimeUtc(info.LocalPath);
            }

            if (FileSystemHelpers.FileExists(Path.Combine(localPath, _applicationHostFile)))
            {
                SetLocalExtensionUrl(info);
            }
            else if (IsPreInstalledExtensionEnabledInPortal(info.Id))
            {
                info.ExtensionUrl = _baseUrl + info.ExtensionUrl + "/";
            }
            else
            {
                info.ExtensionUrl = null;
            }
        }

        private void SetLocalExtensionUrl(SiteExtensionInfo info)
        {
            try
            {
                var appHostDoc = new XmlDocument();
                appHostDoc.Load(Path.Combine(info.LocalPath, _applicationHostFile));

                // Get the 'path' property of the first 'application' element, which is the relative url.
                XmlNode appNode = appHostDoc.SelectSingleNode("//application[@path]/@path");

                info.ExtensionUrl = _baseUrl + appNode.Value + "/";
            }
            catch (SystemException)
            {
                info.ExtensionUrl = null;
            }
        }

        private SiteExtensionInfo ConvertRemotePackageToSiteExtensionInfo(IPackage package)
        {
            var info = new SiteExtensionInfo(package);

            IPackage localPackage = _localRepository.FindPackage(info.Id);
            if (localPackage != null)
            {
                SetLocalInfo(info);
                // Assume input package (from remote) is always the latest version.
                info.LocalIsLatestVersion = package.Version == localPackage.Version;
            }

            return info;
        }

        private SiteExtensionInfo ConvertLocalPackageToSiteExtensionInfo(IPackage package, bool checkLatest = true)
        {
            var info = new SiteExtensionInfo(package);

            SetLocalInfo(info);

            if (checkLatest)
            {
                // FindPackage gets back the latest version.
                IPackage latestPackage = _remoteRepository.FindPackage(info.Id);
                if (latestPackage != null)
                {
                    info.LocalIsLatestVersion = package.Version == latestPackage.Version;
                    info.DownloadCount = package.DownloadCount;
                }
            }



            return info;
        }

        private static bool IsPreInstalledExtensionEnabledInPortal(string id)
        {
            string appSettingName = id.ToUpper(CultureInfo.CurrentCulture) + "_EXTENSION_VERSION";
            bool enabledInSetting = ConfigurationManager.AppSettings[appSettingName] == "beta";
            return enabledInSetting;
        }

        private static string GetLatestPreInstalledExtensionVersion(string id)
        {
            try
            {
                IEnumerable<string> pathStrings = FileSystemHelpers.GetDirectories("D:\\Program Files (x86)\\SiteExtensions\\" + id);
                SemanticVersion maxVersion = pathStrings.Max(path =>
                {
                    string versionString = FileSystemHelpers.DirectoryInfoFromDirectoryName(path).Name;
                    SemanticVersion semVer;
                    if (SemanticVersion.TryParse(versionString, out semVer))
                    {
                        return semVer;
                    }
                    else
                    {
                        return new SemanticVersion(0, 0, 0, 0);
                    }
                });
                return maxVersion.ToString();
            }
            catch (IOException)
            {
                return null;
            }
        }
    }
}
