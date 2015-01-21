using Kudu.Contracts.Jobs;
using Kudu.Contracts.Settings;
using Kudu.Contracts.SiteExtensions;
using Kudu.Contracts.Tracing;
using Kudu.Core.Deployment.Generator;
using Kudu.Core.Infrastructure;
using Kudu.Core.Settings;
using Kudu.Core.Tracing;
using Newtonsoft.Json;
using NuGet.Client;
using NuGet.Client.VisualStudio;
using NuGet.Versioning;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using NullLogger = Kudu.Core.Deployment.NullLogger;

namespace Kudu.Core.SiteExtensions
{
    public class SiteExtensionManager : ISiteExtensionManager
    {
        private static Lazy<IEnumerable<Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>>> _providers
            = new Lazy<IEnumerable<Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>>>(GetNugetProviders);

        private readonly SourceRepository _localRepository;
        private readonly IEnvironment _environment;
        private readonly IDeploymentSettingsManager _settings;
        private readonly ITraceFactory _traceFactory;

        private const string _applicationHostFile = "applicationHost.xdt";
        private const string _settingsFileName = "SiteExtensionSettings.json";
        private const string _feedUrlSetting = "feed_url";

        private readonly string _rootPath;
        private readonly string _baseUrl;
        private readonly IContinuousJobsManager _continuousJobManager;
        private readonly ITriggeredJobsManager _triggeredJobManager;

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

        public SiteExtensionManager(IContinuousJobsManager continuousJobManager, ITriggeredJobsManager triggeredJobManager, IEnvironment environment, IDeploymentSettingsManager settings, ITraceFactory traceFactory, HttpContextBase context)
        {
            _rootPath = Path.Combine(environment.RootPath, "SiteExtensions");
            _baseUrl = context.Request.Url == null ? String.Empty : context.Request.Url.GetLeftPart(UriPartial.Authority).TrimEnd('/');

            _localRepository = GetSourceRepository(_rootPath);
            _continuousJobManager = continuousJobManager;
            _triggeredJobManager = triggeredJobManager;
            _environment = environment;
            _settings = settings;
            _traceFactory = traceFactory;
        }

        public async Task<IEnumerable<SiteExtensionInfo>> GetRemoteExtensions(string filter, bool allowPrereleaseVersions, string feedUrl)
        {
            var extensions = new List<SiteExtensionInfo>(GetPreInstalledExtensions(filter, showEnabledOnly: false));
            SourceRepository remoteRepo = GetRemoteRepository(feedUrl);

            SearchFilter filterOptions = new SearchFilter();
            filterOptions.IncludePrerelease = allowPrereleaseVersions;
            IEnumerable<UISearchMetadata> packages =
                (await remoteRepo.Search(string.IsNullOrWhiteSpace(filter) ? string.Empty : filter, filterOptions: filterOptions))
                    .OrderByDescending(p => p.LatestPackageMetadata.DownloadCount);

            foreach (UISearchMetadata package in packages)
            {
                extensions.Add(await ConvertRemotePackageToSiteExtensionInfo(package, feedUrl));
            }

            return extensions;
        }

        public async Task<SiteExtensionInfo> GetRemoteExtension(string id, string version, string feedUrl)
        {
            SiteExtensionInfo info = GetPreInstalledExtension(id);
            if (info != null)
            {
                return info;
            }

            SourceRepository remoteRepo = GetRemoteRepository(feedUrl);
            UIPackageMetadata package =
                string.IsNullOrWhiteSpace(version) ?
                    await remoteRepo.GetLatestPackageById(id) :
                    await remoteRepo.GetPackageByIdentity(id, version);

            if (package == null)
            {
                return null;
            }

            info = await ConvertRemotePackageToSiteExtensionInfo(package, feedUrl);

            return info;
        }

        public async Task<IEnumerable<SiteExtensionInfo>> GetLocalExtensions(string filter, bool checkLatest)
        {
            IEnumerable<SiteExtensionInfo> preInstalledExtensions = GetPreInstalledExtensions(filter, showEnabledOnly: true);
            IEnumerable<UISearchMetadata> searchResult = await _localRepository.Search(filter);

            List<SiteExtensionInfo> siteExtensionInfos = new List<SiteExtensionInfo>();
            foreach (var item in searchResult)
            {
                siteExtensionInfos.Add(await ConvertLocalPackageToSiteExtensionInfo(item, checkLatest));
            }

            return preInstalledExtensions.Concat(siteExtensionInfos);
        }

        public async Task<SiteExtensionInfo> GetLocalExtension(string id, bool checkLatest = true)
        {
            SiteExtensionInfo info = GetPreInstalledExtension(id);
            if (info != null && info.ExtensionUrl != null)
            {
                return info;
            }

            UIPackageMetadata package = await _localRepository.GetLatestPackageById(id);
            if (package == null)
            {
                return null;
            }

            return await ConvertLocalPackageToSiteExtensionInfo(package, checkLatest);
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

        public async Task<SiteExtensionInfo> InstallExtension(string id, string version, string feedUrl)
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

                SourceRepository remoteRepo = GetRemoteRepository(feedUrl);
                UIPackageMetadata localPackage = null;
                UIPackageMetadata repoPackage =
                    string.IsNullOrWhiteSpace(version) ?
                        await remoteRepo.GetLatestPackageById(id) :
                        await remoteRepo.GetPackageByIdentity(id, version);

                if (repoPackage != null)
                {
                    string installationDirectory = GetInstallationDirectory(id);

                    localPackage = await InstallExtension(repoPackage, installationDirectory, feedUrl);
                    GetSettingManager(id).SetValue(_feedUrlSetting, feedUrl);
                }

                return await ConvertLocalPackageToSiteExtensionInfo(localPackage, checkLatest: true);
            }
        }

        private async Task<UIPackageMetadata> InstallExtension(UIPackageMetadata package, string installationDirectory, string feedUrl)
        {
            try
            {
                if (FileSystemHelpers.DirectoryExists(installationDirectory))
                {
                    FileSystemHelpers.DeleteDirectorySafe(installationDirectory);
                }

                SourceRepository remoteRepo = GetRemoteRepository(feedUrl);

                // copy content folder
                await remoteRepo.DownloadPackageToFolder(package.Identity, installationDirectory);

                // If there is no xdt file, generate default.
                GenerateApplicationHostXdt(installationDirectory, '/' + package.Identity.Id, isPreInstalled: false);

                OperationManager.Attempt(() => DeploySiteExtensionJobs(package.Identity.Id));

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
                string packageFilePath = GetNuGetPackageFile(package.Identity.Id, package.Identity.Version.ToString());
                var downloadResource = await remoteRepo.GetResourceAsync<DownloadResource>();
                using (
                    Stream readStream = await downloadResource.GetStream(package.Identity, CancellationToken.None),
                    writeStream = FileSystemHelpers.OpenWrite(packageFilePath))
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

            return await _localRepository.GetLatestPackageById(package.Identity.Id);
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

        public async Task<bool> UninstallExtension(string id)
        {
            string installationDirectory = GetInstallationDirectory(id);

            SiteExtensionInfo info = await GetLocalExtension(id, checkLatest: false);

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

            OperationManager.Attempt(() => CleanupSiteExtensionJobs(id));

            OperationManager.Attempt(() => FileSystemHelpers.DeleteFileSafe(GetNuGetPackageFile(info.Id, info.Version)));

            OperationManager.Attempt(() => FileSystemHelpers.DeleteDirectorySafe(installationDirectory));

            return await GetLocalExtension(id, checkLatest: false) == null;
        }

        private SourceRepository GetRemoteRepository(string feedUrl)
        {
            return string.IsNullOrWhiteSpace(feedUrl) ?
                GetSourceRepository(_settings.GetSiteExtensionRemoteUrl()) :
                GetSourceRepository(feedUrl);
        }

        private string GetInstallationDirectory(string id)
        {
            return Path.Combine(_localRepository.PackageSource.Source, id);
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
            string template = null;

            Stream stream = typeof(SiteExtensionManager).Assembly.GetManifestResourceStream("Kudu.Core.SiteExtensions." + _applicationHostFile + ".xml");
            using (StreamReader reader = new StreamReader(stream))
            {
                template = reader.ReadToEnd();
            }

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

        private void DeploySiteExtensionJobs(string siteExtensionName)
        {
            string siteExtensionPath = Path.Combine(_rootPath, siteExtensionName);
            _continuousJobManager.SyncExternalJobs(siteExtensionPath, siteExtensionName);
            _triggeredJobManager.SyncExternalJobs(siteExtensionPath, siteExtensionName);
        }

        private void CleanupSiteExtensionJobs(string siteExtensionName)
        {
            _continuousJobManager.CleanupExternalJobs(siteExtensionName);
            _triggeredJobManager.CleanupExternalJobs(siteExtensionName);
        }

        private async Task<SiteExtensionInfo> ConvertRemotePackageToSiteExtensionInfo(UISearchMetadata package, string feedUrl)
        {
            return await CheckRemotePackageLatestVersion(new SiteExtensionInfo(package), feedUrl);
        }

        private async Task<SiteExtensionInfo> ConvertRemotePackageToSiteExtensionInfo(UIPackageMetadata package, string feedUrl)
        {
            return await CheckRemotePackageLatestVersion(new SiteExtensionInfo(package), feedUrl);
        }

        private async Task<SiteExtensionInfo> CheckRemotePackageLatestVersion(SiteExtensionInfo info, string feedUrl)
        {
            info.FeedUrl = feedUrl;
            UIPackageMetadata localPackage = await _localRepository.GetLatestPackageById(info.Id);

            if (localPackage != null)
            {
                SetLocalInfo(info);
                // Assume input package (from remote) is always the latest version.
                info.LocalIsLatestVersion = NuGetVersion.Parse(info.Version).Equals(localPackage.Identity.Version);
            }

            return info;
        }

        private async Task<SiteExtensionInfo> ConvertLocalPackageToSiteExtensionInfo(UIPackageMetadata package, bool checkLatest)
        {
            if (package == null)
            {
                return null;
            }

            var info = new SiteExtensionInfo(package);
            SetLocalInfo(info);
            await TryCheckLocalPackageLatestVersionFromRemote(info, checkLatest);
            return info;
        }

        private async Task<SiteExtensionInfo> ConvertLocalPackageToSiteExtensionInfo(UISearchMetadata package, bool checkLatest)
        {
            if (package == null)
            {
                return null;
            }

            var info = new SiteExtensionInfo(package);
            SetLocalInfo(info);
            await TryCheckLocalPackageLatestVersionFromRemote(info, checkLatest);
            return info;
        }

        private async Task TryCheckLocalPackageLatestVersionFromRemote(SiteExtensionInfo info, bool checkLatest)
        {
            if (checkLatest)
            {
                // FindPackage gets back the latest version.
                SourceRepository remoteRepo = GetRemoteRepository(info.FeedUrl);
                UIPackageMetadata latestPackage = await remoteRepo.GetLatestPackageById(info.Id);
                if (latestPackage != null)
                {
                    NuGetVersion currentVersion = NuGetVersion.Parse(info.Version);
                    info.LocalIsLatestVersion = NuGetVersion.Parse(info.Version).Equals(latestPackage.Identity.Version);
                    info.DownloadCount = latestPackage.DownloadCount;
                    info.PublishedDateTime = latestPackage.Published;
                }
            }
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

            if (pathStrings.Length == 0)
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

        /// <summary>
        /// Create SourceRepository from given feed endpoint
        /// </summary>
        /// <param name="feedEndpoint">V2 or V3 feed endpoint</param>
        /// <returns>SourceRepository object</returns>
        private static SourceRepository GetSourceRepository(string feedEndpoint)
        {
            NuGet.Configuration.PackageSource source = new NuGet.Configuration.PackageSource(feedEndpoint);
            return new SourceRepository(source, _providers.Value);
        }

        private static IEnumerable<Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>> GetNugetProviders()
        {
            var aggregateCatalog = new AggregateCatalog();
            var directoryCatalog = new DirectoryCatalog(HttpRuntime.BinDirectory, "NuGet.Client*.dll");
            aggregateCatalog.Catalogs.Add(directoryCatalog);
            var container = new CompositionContainer(aggregateCatalog);
            return container.GetExports<INuGetResourceProvider, INuGetResourceProviderMetadata>();
        }
    }
}
