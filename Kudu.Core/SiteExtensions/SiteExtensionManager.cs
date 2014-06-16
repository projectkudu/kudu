using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Web;
using System.Xml;
using Kudu.Contracts.Settings;
using Kudu.Contracts.SiteExtensions;
using Kudu.Contracts.Tracing;
using Kudu.Core.Deployment.Generator;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;
using Newtonsoft.Json;
using NuGet;
using NullLogger = Kudu.Core.Deployment.NullLogger;

namespace Kudu.Core.SiteExtensions
{
    public class SiteExtensionManager : ISiteExtensionManager
    {
        private readonly IPackageRepository _remoteRepository;
        private readonly IPackageRepository _localRepository;
        private readonly IEnvironment _environment;
        private readonly IDeploymentSettingsManager _settings;
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
                    Id = "Monaco",
                    Title = "Visual Studio Online",
                    Type = SiteExtensionInfo.SiteExtensionType.PreInstalledNonKudu,
                    Authors = new [] {"Microsoft"},
                    IconUrl = "https://www.siteextensions.net/Content/Images/vso50x50.png",
                    LicenseUrl = "http://azure.microsoft.com/en-us/support/legal/",
                    ProjectUrl = "http://blogs.msdn.com/b/monaco/",
                    Description = "A full featured browser based development environment for editing your website",
                    // API will return a full url instead of this relative url.
                    ExtensionUrl = "/Dev"
                }
            },
            {
                "kudu",
                new SiteExtensionInfo
                {
                    Id = "Kudu",
                    Title = "Site Admin Tools",
                    Type = SiteExtensionInfo.SiteExtensionType.PreInstalledKuduModule,
                    Authors = new [] {"Project Kudu Team"},
                    IconUrl = "https://www.siteextensions.net/Content/Images/kudu50x50.png",
                    LicenseUrl = "https://github.com/projectkudu/kudu/blob/master/LICENSE.txt",
                    ProjectUrl = "https://github.com/projectkudu/kudu",
                    Description = "Site administration and management tools for your Azure Websites, including a terminal, process viewer and more!",
                    // API will return a full url instead of this relative url.
                    ExtensionUrl = "/"
                }
            }
        };

        private const string _installScriptName = "install.cmd";
        private const string _uninstallScriptName = "uninstall.cmd";

        public SiteExtensionManager(IEnvironment environment, IDeploymentSettingsManager settings, ITraceFactory traceFactory, HttpContextBase context)
        {
            _localRepository = new LocalPackageRepository(environment.RootPath + "\\SiteExtensions");
            _environment = environment;
            _settings = settings;
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

        private IEnumerable<SiteExtensionInfo> GetPreInstalledExtensions(string filter, bool showEnabledOnly)
        {
            var list = new List<SiteExtensionInfo>();

            foreach (SiteExtensionInfo extension in _preInstalledExtensionDictionary.Values)
            {
                if (String.IsNullOrEmpty(filter) ||
                    JsonConvert.SerializeObject(extension).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    SiteExtensionInfo info = GetPreInstalledExtension(extension.Id);

                    if (!showEnabledOnly || info.ExtensionUrl != null)
                    {
                        list.Add(info);
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

                SetPreInstalledExtensionInfo(info);

                return info;
            }
            else
            {
                return null;
            }
        }

        public SiteExtensionInfo InstallExtension(string id, string version)
        {
            if (_preInstalledExtensionDictionary.ContainsKey(id))
            {
                return EnablePreInstalledExtension(_preInstalledExtensionDictionary[id]);
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

                var externalCommandFactory = new ExternalCommandFactory(_environment, _settings, installationDirectory);
                string installScript = Path.Combine(installationDirectory, _installScriptName);
                if (FileSystemHelpers.FileExists(installScript))
                {
                    Executable exe = externalCommandFactory.BuildCommandExecutable(installScript, installationDirectory,
                        _settings.GetCommandIdleTimeout(), NullLogger.Instance);
                    exe.ExecuteWithProgressWriter(NullLogger.Instance, _traceFactory.GetTracer(), String.Empty);
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

        private SiteExtensionInfo EnablePreInstalledExtension(SiteExtensionInfo info)
        {
            string id = info.Id;
            string installationDirectory = GetInstallationDirectory(id);

            try
            {
                if (FileSystemHelpers.DirectoryExists(installationDirectory))
                {
                    FileSystemHelpers.DeleteDirectorySafe(installationDirectory);
                }

                if (ExtensionRequiresApplicationHost(info))
                {
                    if (info.Type == SiteExtensionInfo.SiteExtensionType.PreInstalledNonKudu)
                    {
                        GenerateApplicationHostXdt(installationDirectory,
                            _preInstalledExtensionDictionary[id].ExtensionUrl, isPreInstalled: true);
                    }
                }
                else
                {
                    FileSystemHelpers.CreateDirectory(installationDirectory);
                }
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

            OperationManager.Attempt(() =>
            {
                var externalCommandFactory = new ExternalCommandFactory(_environment, _settings, installationDirectory);
                string uninstallScript = Path.Combine(installationDirectory, _uninstallScriptName);
                if (FileSystemHelpers.FileExists(uninstallScript))
                {
                    Executable exe = externalCommandFactory.BuildCommandExecutable(uninstallScript, installationDirectory,
                        _settings.GetCommandIdleTimeout(), NullLogger.Instance);
                    exe.ExecuteWithProgressWriter(NullLogger.Instance, _traceFactory.GetTracer(), String.Empty);
                }
                
                FileSystemHelpers.DeleteDirectorySafe(installationDirectory);
            });

            return !FileSystemHelpers.DirectoryExists(installationDirectory);
        }

        private string GetInstallationDirectory(string id)
        {
            return Path.Combine(_localRepository.Source, id);
        }

        private static string GetPreInstalledDirectory(string id)
        {
            string programFiles = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86);
            return Path.Combine(programFiles, "SiteExtensions", id);
        }

        private static string CreateDefaultXdtFile(string relativeUrl, bool isPreInstalled)
        {
            // Hack - will be reverted.
            string physicalPath = "%XDT_EXTENSIONPATH%"; // "%XDT_LATEST_EXTENSIONPATH%"
            if (isPreInstalled && relativeUrl == "/dev")
            {
                string id = "Monaco";
                string directory = GetPreInstalledDirectory(id);
                string version = GetPreInstalledLatestVersion(directory);
                physicalPath = Path.Combine(directory, version);
            }
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

            if (ExtensionRequiresApplicationHost(info))
            {
                if (FileSystemHelpers.FileExists(Path.Combine(localPath, _applicationHostFile)))
                {
                    info.ExtensionUrl = GetUrlFromApplicationHost(info);
                }
                else
                {
                    info.ExtensionUrl = null;
                }
            }
            else
            {
                if (!String.IsNullOrEmpty(info.LocalPath))
                {
                    info.ExtensionUrl = _baseUrl + info.ExtensionUrl + "/";
                }
                else
                {
                    info.ExtensionUrl = null;
                }
            }
        }

        private string GetUrlFromApplicationHost(SiteExtensionInfo info)
        {
            try
            {
                var appHostDoc = new XmlDocument();
                appHostDoc.Load(Path.Combine(info.LocalPath, _applicationHostFile));

                // Get the 'path' property of the first 'application' element, which is the relative url.
                XmlNode pathPropertyNode = appHostDoc.SelectSingleNode("//application[@path]/@path");

                return _baseUrl + pathPropertyNode.Value + "/";
            }
            catch (SystemException)
            {
                return null;
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
                    info.PublishedDateTime = package.Published;
                }
            }

            return info;
        }

        private static bool ExtensionRequiresApplicationHost(SiteExtensionInfo info)
        {
            string appSettingName = info.Id.ToUpper(CultureInfo.CurrentCulture) + "_EXTENSION_VERSION";
            bool enabledInSetting = ConfigurationManager.AppSettings[appSettingName] == "beta";
            return !(enabledInSetting || info.Type == SiteExtensionInfo.SiteExtensionType.PreInstalledKuduModule);
        }

        private static void SetPreInstalledExtensionInfo(SiteExtensionInfo info)
        {
            try
            {
                if (info.Type == SiteExtensionInfo.SiteExtensionType.PreInstalledNonKudu)
                {
                    string directory = GetPreInstalledDirectory(info.Id);
                    info.Version = GetPreInstalledLatestVersion(directory);
                }
                else if (info.Type == SiteExtensionInfo.SiteExtensionType.PreInstalledKuduModule)
                {
                    info.Version = typeof(SiteExtensionManager).Assembly.GetName().Version.ToString();
                }
            }
            catch (IOException)
            {
                info.Version = null;
            }

            info.LocalIsLatestVersion = true;
        }

        private static string GetPreInstalledLatestVersion(string directory)
        {
            IEnumerable<string> pathStrings = FileSystemHelpers.GetDirectories(directory);

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
    }
}
