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
using Kudu.Core.Deployment.Generator;
using Kudu.Core.Infrastructure;
using Kudu.Core.Settings;
using Kudu.Core.Tracing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet;
using NullLogger = Kudu.Core.Deployment.NullLogger;

namespace Kudu.Core.SiteExtensions
{
    public class SiteExtensionManager : ISiteExtensionManager
    {
        private readonly IPackageRepository _localRepository;
        private readonly IEnvironment _environment;
        private readonly IDeploymentSettingsManager _settings;
        private readonly ITraceFactory _traceFactory;

        private const string _applicationHostFile = "applicationHost.xdt";
        private const string _settingsFileName = "SiteExtensionSettings.json";
        private const string _feedUrlSetting = "feed_url";

        private readonly string _rootPath;
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
                    Type = SiteExtensionInfo.SiteExtensionType.PreInstalledMonaco,
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
                    Type = SiteExtensionInfo.SiteExtensionType.PreInstalledEnabled,
                    Authors = new [] {"Project Kudu Team"},
                    IconUrl = "https://www.siteextensions.net/Content/Images/kudu50x50.png",
                    LicenseUrl = "https://github.com/projectkudu/kudu/blob/master/LICENSE.txt",
                    ProjectUrl = "https://github.com/projectkudu/kudu",
                    Description = "Site administration and management tools for your Azure Websites, including a terminal, process viewer and more!",
                    // API will return a full url instead of this relative url.
                    ExtensionUrl = "/"
                }
            },
            {
                "daas",
                new SiteExtensionInfo
                {
                    Id = "Daas",
                    Title = "Diagnostics as a Service",
                    Type = SiteExtensionInfo.SiteExtensionType.PreInstalledEnabled,
                    Authors = new [] {"Microsoft"},
                    IconUrl = "https://www.siteextensions.net/Content/Images/DaaS50x50.png",
                    LicenseUrl = "http://azure.microsoft.com/en-us/support/legal/",
                    ProjectUrl = "http://azure.microsoft.com/blog/?p=157471",
                    Description = "Site diagnostic tools, including Event Viewer logs, memory dumps and http logs.",
                    // API will return a full url instead of this relative url.
                    ExtensionUrl = "/DaaS"
                }
            }
        };

        private const string _installScriptName = "install.cmd";
        private const string _uninstallScriptName = "uninstall.cmd";

        public SiteExtensionManager(IEnvironment environment, IDeploymentSettingsManager settings, ITraceFactory traceFactory, HttpContextBase context)
        {
            _rootPath = Path.Combine(environment.RootPath, "SiteExtensions");
            _baseUrl = context.Request.Url == null ? String.Empty : context.Request.Url.GetLeftPart(UriPartial.Authority).TrimEnd('/');
            _localRepository = new LocalPackageRepository(_rootPath);
            _environment = environment;
            _settings = settings;
            _traceFactory = traceFactory;
        }

        public IEnumerable<SiteExtensionInfo> GetRemoteExtensions(string filter, bool allowPrereleaseVersions, string feedUrl)
        {
            var extensions = new List<SiteExtensionInfo>(GetPreInstalledExtensions(filter, showEnabledOnly: false));

            IPackageRepository remoteRepository = GetRemoteRepository(feedUrl);

            IQueryable<IPackage> packages = String.IsNullOrEmpty(filter) ?
                remoteRepository.GetPackages().Where(p => p.IsLatestVersion).OrderByDescending(p => p.DownloadCount) :
                remoteRepository.Search(filter, allowPrereleaseVersions).Where(p => p.IsLatestVersion);

            foreach (IPackage package in packages)
            {
                extensions.Add(ConvertRemotePackageToSiteExtensionInfo(package, feedUrl));
            }

            return extensions;
        }

        public SiteExtensionInfo GetRemoteExtension(string id, string version, string feedUrl)
        {
            SiteExtensionInfo info = GetPreInstalledExtension(id);
            if (info != null)
            {
                return info;
            }

            var semanticVersion = version == null ? null : new NuGet.SemanticVersion(version);

            IPackage package = GetRemoteRepository(feedUrl).FindPackage(id, semanticVersion);
            if (package == null)
            {
                return null;
            }

            info = ConvertRemotePackageToSiteExtensionInfo(package, feedUrl);

            return info;
        }

        public IEnumerable<SiteExtensionInfo> GetLocalExtensions(string filter, bool checkLatest)
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

        public SiteExtensionInfo InstallExtension(string id, string version, string feedUrl)
        {
            if (_preInstalledExtensionDictionary.ContainsKey(id))
            {
                return EnablePreInstalledExtension(_preInstalledExtensionDictionary[id]);
            }
            else
            {
                if (String.IsNullOrEmpty(feedUrl))
                {
                    feedUrl = GetSettingManager(id).GetValue(_feedUrlSetting);
                }

                IPackageRepository remoteRepository = GetRemoteRepository(feedUrl);
                IPackage localPackage = null;
                IPackage repoPackage = version == null ? remoteRepository.FindPackage(id) :
                    remoteRepository.FindPackage(id, new NuGet.SemanticVersion(version), allowPrereleaseVersions: true, allowUnlisted: true);

                if (repoPackage != null)
                {
                    string installationDirectory = GetInstallationDirectory(id);
                    localPackage = InstallExtension(repoPackage, installationDirectory);
                    GetSettingManager(id).SetValue(_feedUrlSetting, feedUrl);
                }

                return ConvertLocalPackageToSiteExtensionInfo(localPackage, checkLatest: true);
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

                var externalCommandFactory = new ExternalCommandFactory(_environment, _settings, installationDirectory);
                string installScript = Path.Combine(installationDirectory, _installScriptName);
                if (FileSystemHelpers.FileExists(installScript))
                {
                    OperationManager.Attempt(() =>
                    {
                        Executable exe = externalCommandFactory.BuildCommandExecutable(installScript,
                            installationDirectory,
                            _settings.GetCommandIdleTimeout(), NullLogger.Instance);
                        exe.ExecuteWithProgressWriter(NullLogger.Instance, _traceFactory.GetTracer(), String.Empty);
                    });
                }

                // Copy nupkg file for package list/lookup
                FileSystemHelpers.CreateDirectory(installationDirectory);
                string packageFilePath = GetNuGetPackageFile(package.Id, package.Version.ToString());
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
                throw;
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
                    if (info.Type == SiteExtensionInfo.SiteExtensionType.PreInstalledMonaco)
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

            SiteExtensionInfo info = GetLocalExtension(id, checkLatest: false);

            if (info == null || !FileSystemHelpers.DirectoryExists(info.LocalPath))
            {
                throw new DirectoryNotFoundException(installationDirectory);
            }

            var externalCommandFactory = new ExternalCommandFactory(_environment, _settings, installationDirectory);

            string uninstallScript = Path.Combine(installationDirectory, _uninstallScriptName);

            if (FileSystemHelpers.FileExists(uninstallScript))
            {
                OperationManager.Attempt(() =>
                {
                    Executable exe = externalCommandFactory.BuildCommandExecutable(uninstallScript,
                        installationDirectory,
                        _settings.GetCommandIdleTimeout(), NullLogger.Instance);
                    exe.ExecuteWithProgressWriter(NullLogger.Instance, _traceFactory.GetTracer(), String.Empty);
                });
            }

            OperationManager.Attempt(() => FileSystemHelpers.DeleteFileSafe(GetNuGetPackageFile(info.Id, info.Version)));

            OperationManager.Attempt(() => FileSystemHelpers.DeleteDirectorySafe(installationDirectory));

            return GetLocalExtension(id, checkLatest: false) == null;
        }

        private IPackageRepository GetRemoteRepository(string feedUrl)
        {
            return feedUrl == null ? 
                new DataServicePackageRepository(new Uri(_settings.GetSiteExtensionRemoteUrl())) : 
                new DataServicePackageRepository(new Uri(feedUrl));
        }

        private string GetInstallationDirectory(string id)
        {
            return Path.Combine(_localRepository.Source, id);
        }

        private JsonSettings GetSettingManager(string id)
        {
            string filePath = Path.Combine(_rootPath, id, _settingsFileName);

            return new JsonSettings(filePath);
        }

        private string GetNuGetPackageFile(string id, string version)
        {
            return Path.Combine(GetInstallationDirectory(id), String.Format("{0}.{1}.nupkg", id, version));
        }

        private static string GetPreInstalledDirectory(string id)
        {
            string programFiles = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86);
            return Path.Combine(programFiles, "SiteExtensions", id);
        }

        private static string CreateDefaultXdtFile(string relativeUrl, bool isPreInstalled)
        {
            string physicalPath = isPreInstalled ? "%XDT_LATEST_EXTENSIONPATH%" : "%XDT_EXTENSIONPATH%";
            string template = typeof(SiteExtensionManager).Assembly
                .GetManifestResourceStream("Kudu.Core.SiteExtensions." + _applicationHostFile + ".xml").ReadToEnd();
            return String.Format(template, relativeUrl, physicalPath);
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
                info.ExtensionUrl = FileSystemHelpers.FileExists(Path.Combine(localPath, _applicationHostFile)) 
                    ? GetFullUrl(GetUrlFromApplicationHost(info)) : null;
            }
            else if (String.Equals(info.Id, "Monaco", StringComparison.OrdinalIgnoreCase))
            {
                // Monaco does not need ApplicationHost only when it is enabled through app setting 
                info.ExtensionUrl = GetFullUrl(info.ExtensionUrl);
            }
            else
            {
                info.ExtensionUrl = String.IsNullOrEmpty(info.LocalPath) ? null : GetFullUrl(info.ExtensionUrl);
            }

            info.FeedUrl = GetSettingManager(info.Id).GetValue(_feedUrlSetting);
        }

        private static string GetUrlFromApplicationHost(SiteExtensionInfo info)
        {
            try
            {
                var appHostDoc = new XmlDocument();
                appHostDoc.Load(Path.Combine(info.LocalPath, _applicationHostFile));

                // Get the 'path' property of the first 'application' element, which is the relative url.
                XmlNode pathPropertyNode = appHostDoc.SelectSingleNode("//application[@path]/@path");

                return pathPropertyNode.Value;
            }
            catch (SystemException)
            {
                return null;
            }
        }

        private SiteExtensionInfo ConvertRemotePackageToSiteExtensionInfo(IPackage package, string feedUrl)
        {
            var info = new SiteExtensionInfo(package);

            info.FeedUrl = feedUrl;

            IPackage localPackage = _localRepository.FindPackage(info.Id);
            if (localPackage != null)
            {
                SetLocalInfo(info);
                // Assume input package (from remote) is always the latest version.
                info.LocalIsLatestVersion = package.Version == localPackage.Version;
            }

            return info;
        }

        private SiteExtensionInfo ConvertLocalPackageToSiteExtensionInfo(IPackage package, bool checkLatest)
        {
            if (package == null)
            {
                return null;
            }

            var info = new SiteExtensionInfo(package);

            SetLocalInfo(info);

            if (checkLatest)
            {
                // FindPackage gets back the latest version.
                IPackage latestPackage = GetRemoteRepository(info.FeedUrl).FindPackage(info.Id);
                if (latestPackage != null)
                {
                    info.LocalIsLatestVersion = package.Version == latestPackage.Version;
                    info.DownloadCount = latestPackage.DownloadCount;
                    info.PublishedDateTime = latestPackage.Published;
                }
            }

            return info;
        }

        private static bool ExtensionRequiresApplicationHost(SiteExtensionInfo info)
        {
            string appSettingName = info.Id.ToUpper(CultureInfo.CurrentCulture) + "_EXTENSION_VERSION";
            bool enabledInSetting = ConfigurationManager.AppSettings[appSettingName] == "beta";
            return !(enabledInSetting || info.Type == SiteExtensionInfo.SiteExtensionType.PreInstalledEnabled);
        }

        private static void SetPreInstalledExtensionInfo(SiteExtensionInfo info)
        {
            string directory = GetPreInstalledDirectory(info.Id);

            if (FileSystemHelpers.DirectoryExists(directory))
            {
                if (info.Type == SiteExtensionInfo.SiteExtensionType.PreInstalledMonaco)
                {
                    info.Version = GetPreInstalledLatestVersion(directory);
                }
                else if (info.Type == SiteExtensionInfo.SiteExtensionType.PreInstalledEnabled)
                {
                    info.Version = typeof(SiteExtensionManager).Assembly.GetName().Version.ToString();
                }

                info.PublishedDateTime = FileSystemHelpers.GetLastWriteTimeUtc(directory);
            }
            else
            {
                info.Version = null;
                info.PublishedDateTime = null;
            }

            info.LocalIsLatestVersion = true;
        }

        private static string GetPreInstalledLatestVersion(string directory)
        {
            if (!FileSystemHelpers.DirectoryExists(directory))
            {
                return null;
            }

            string[] pathStrings = FileSystemHelpers.GetDirectories(directory);

            if (pathStrings.IsEmpty())
            {
                return null;
            }

            return pathStrings.Max(path =>
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
            }).ToString();
        }

        private string GetFullUrl(string url)
        {
            return url == null ? null : new Uri(new Uri(_baseUrl), url).ToString().Trim('/') + "/";
        }
    }
}
