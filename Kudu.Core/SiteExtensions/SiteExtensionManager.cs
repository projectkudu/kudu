using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.Jobs;
using Kudu.Contracts.Settings;
using Kudu.Contracts.SiteExtensions;
using Kudu.Contracts.Tracing;
using Kudu.Core.Deployment.Generator;
using Kudu.Core.Infrastructure;
using Kudu.Core.Settings;
using Kudu.Core.Tracing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Client;
using NuGet.Client.VisualStudio;
using NuGet.Versioning;
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
        private readonly IAnalytics _analytics;

        private const string _settingsFileName = "SiteExtensionSettings.json";
        private const string _feedUrlSetting = "feed_url";
        private const string _installUtcTimestampSetting = "install_timestamp_utc";
        private const string _packageType = "package_type";
        private const string _versionSetting = "version";

        private readonly string _rootPath;
        private readonly string _baseUrl;
        private readonly IContinuousJobsManager _continuousJobManager;
        private readonly ITriggeredJobsManager _triggeredJobManager;

        private static readonly string _toBeDeletedDirectoryPath = System.Environment.ExpandEnvironmentVariables(@"%HOME%\data\Temp\SiteExtensions");

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

        public SiteExtensionManager(IContinuousJobsManager continuousJobManager, ITriggeredJobsManager triggeredJobManager, IEnvironment environment, IDeploymentSettingsManager settings, ITraceFactory traceFactory, HttpContextBase context, IAnalytics analytics)
        {
            _rootPath = Path.Combine(environment.RootPath, "SiteExtensions");
            _baseUrl = context.Request.Url == null ? String.Empty : context.Request.Url.GetLeftPart(UriPartial.Authority).TrimEnd('/');

            _localRepository = GetSourceRepository(_rootPath);
            _continuousJobManager = continuousJobManager;
            _triggeredJobManager = triggeredJobManager;
            _environment = environment;
            _settings = settings;
            _traceFactory = traceFactory;
            _analytics = analytics;
        }

        public async Task<IEnumerable<SiteExtensionInfo>> GetRemoteExtensions(string filter, bool allowPrereleaseVersions, string feedUrl)
        {
            ITracer tracer = _traceFactory.GetTracer();
            var extensions = new List<SiteExtensionInfo>(GetPreInstalledExtensions(filter, showEnabledOnly: false));
            SourceRepository remoteRepo = GetRemoteRepository(feedUrl);

            SearchFilter filterOptions = new SearchFilter();
            filterOptions.IncludePrerelease = allowPrereleaseVersions;

            IEnumerable<UIPackageMetadata> packages = null;

            using (tracer.Step("Search site extensions by filter: {0}", filter))
            {
                packages = (await remoteRepo.Search(string.IsNullOrWhiteSpace(filter) ? string.Empty : filter, filterOptions: filterOptions))
                            .OrderByDescending(p => p.DownloadCount);
            }

            using (tracer.Step("Convert search result to SiteExtensionInfos"))
            {
                var convertedResult = await ConvertNuGetPackagesToSiteExtensionInfos(
                    packages,
                    async (uiPackage) =>
                    {
                        return await ConvertRemotePackageToSiteExtensionInfo(uiPackage, feedUrl);
                    });

                extensions.AddRange(convertedResult);
            }

            return extensions;
        }

        public async Task<SiteExtensionInfo> GetRemoteExtension(string id, string version, string feedUrl)
        {
            ITracer tracer = _traceFactory.GetTracer();

            SiteExtensionInfo info = GetPreInstalledExtension(id);
            if (info != null)
            {
                return info;
            }

            SourceRepository remoteRepo = GetRemoteRepository(feedUrl);
            UIPackageMetadata package = null;

            if (string.IsNullOrWhiteSpace(version))
            {
                using (tracer.Step("Version is null, search latest package by id: {0}", id))
                {
                    package = await remoteRepo.GetLatestPackageById(id);
                }
            }
            else
            {
                using (tracer.Step("Search package by id: {0} and version: {1}", id, version))
                {
                    package = await remoteRepo.GetPackageByIdentity(id, version);
                }
            }

            if (package == null)
            {
                tracer.Trace("No package found with id: {0} and version: {1}", id, version);
                return null;
            }

            return await ConvertRemotePackageToSiteExtensionInfo(package, feedUrl);
        }

        public async Task<IEnumerable<SiteExtensionInfo>> GetLocalExtensions(string filter, bool checkLatest)
        {
            ITracer tracer = _traceFactory.GetTracer();
            IEnumerable<SiteExtensionInfo> preInstalledExtensions = GetPreInstalledExtensions(filter, showEnabledOnly: true);
            IEnumerable<UIPackageMetadata> searchResult = null;
            List<SiteExtensionInfo> siteExtensionInfos = new List<SiteExtensionInfo>();

            foreach (var item in preInstalledExtensions)
            {
                SiteExtensionStatus armSettings = new SiteExtensionStatus(_environment.SiteExtensionSettingsPath, item.Id, tracer);
                armSettings.FillSiteExtensionInfo(item, defaultProvisionState: Constants.SiteExtensionProvisioningStateSucceeded);
            }

            using (tracer.Step("Search packages locally with filter: {0}", filter))
            {
                searchResult = await _localRepository.Search(filter);
                siteExtensionInfos = await ConvertNuGetPackagesToSiteExtensionInfos(
                    searchResult,
                    async (uiPackage) =>
                    {
                        SiteExtensionInfo info = await ConvertLocalPackageToSiteExtensionInfo(uiPackage, checkLatest, tracer);
                        SiteExtensionStatus armSettings = new SiteExtensionStatus(_environment.SiteExtensionSettingsPath, info.Id, tracer);
                        armSettings.FillSiteExtensionInfo(info, defaultProvisionState: Constants.SiteExtensionProvisioningStateSucceeded);
                        return info;
                    });
            }

            return preInstalledExtensions.Concat(siteExtensionInfos);
        }

        // <inheritdoc />
        public async Task<SiteExtensionInfo> GetLocalExtension(string id, bool checkLatest = true)
        {
            ITracer tracer = _traceFactory.GetTracer();
            SiteExtensionInfo info = GetPreInstalledExtension(id);
            if (info != null && info.ExtensionUrl != null)
            {
                tracer.Trace("Pre-installed site extension found: {0}", id);
            }
            else
            {
                UIPackageMetadata package = null;
                using (tracer.Step("{0} is not a pre-installed package. Now querying from local repo.", id))
                {
                    package = await _localRepository.GetLatestPackageById(id);
                }

                if (package == null)
                {
                    tracer.Trace("No package found from local repo with id: {0}.", id);
                    return null;
                }

                using (tracer.Step("Converting NuGet object to SiteExtensionInfo"))
                {
                    info = await ConvertLocalPackageToSiteExtensionInfo(package, checkLatest, tracer: tracer);
                }
            }

            SiteExtensionStatus armSettings = new SiteExtensionStatus(_environment.SiteExtensionSettingsPath, id, tracer);
            armSettings.FillSiteExtensionInfo(info);
            return info;
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

        // <inheritdoc />
        public async Task<SiteExtensionInfo> InstallExtension(string id, string version, string feedUrl, SiteExtensionInfo.SiteExtensionType type, ITracer tracer)
        {
            try
            {
                var installationLock = SiteExtensionInstallationLock.CreateLock(_environment.SiteExtensionSettingsPath, id);
                // hold on to lock till action complete (success or fail)
                return await installationLock.LockOperationAsync<SiteExtensionInfo>(async () =>
                {
                    return await TryInstallExtension(id, version, feedUrl, type, tracer);
                }, TimeSpan.Zero);
            }
            catch (Exception ex)
            {
                _analytics.UnexpectedException(
                        ex,
                        method: "PUT",
                        path: string.Format(CultureInfo.InvariantCulture, "/api/siteextensions/{0}", id),
                        result: Constants.SiteExtensionProvisioningStateFailed,
                        message: string.Format(CultureInfo.InvariantCulture, "{{\"version\": {0}, \"feed_url\": {1}}}", version, feedUrl),
                        trace: false);

                // handle unexpected exception
                tracer.TraceError(ex);

                var info = new SiteExtensionInfo();
                info.Id = id;

                SiteExtensionStatus armStatus = new SiteExtensionStatus(_environment.SiteExtensionSettingsPath, id, tracer);
                armStatus.Operation = Constants.SiteExtensionOperationInstall;
                armStatus.ProvisioningState = Constants.SiteExtensionProvisioningStateFailed;
                armStatus.Status = HttpStatusCode.BadRequest;
                armStatus.FillSiteExtensionInfo(info);
                tracer.Trace("Update arm settings for {0} installation. Status: {1}", id, armStatus.Status);
                return info;
            }
        }

        private async Task<SiteExtensionInfo> TryInstallExtension(string id, string version, string feedUrl, SiteExtensionInfo.SiteExtensionType type, ITracer tracer)
        {
            SiteExtensionInfo info = null;
            HttpStatusCode status = HttpStatusCode.OK;  // final status when success
            bool alreadyInstalled = false;

            if (_preInstalledExtensionDictionary.ContainsKey(id))
            {
                tracer.Trace("Pre-installed site extension found: {0}, not going to perform new installation.", id);
                info = EnablePreInstalledExtension(_preInstalledExtensionDictionary[id], tracer);
                alreadyInstalled = true;
            }
            else
            {
                try
                {
                    // Check if site extension already installed (id, version, feedUrl), if already install return right away
                    if (await this.IsSiteExtensionInstalled(id, version, feedUrl))
                    {
                        // package already installed, return package from local repo.
                        tracer.Trace("Package {0} with version {1} from {2} already installed.", id, version, feedUrl);
                        info = await GetLocalExtension(id);
                        alreadyInstalled = true;
                    }
                    else
                    {
                        JsonSettings siteExtensionSettings = GetSettingManager(id);
                        feedUrl = (string.IsNullOrEmpty(feedUrl) ? siteExtensionSettings.GetValue(_feedUrlSetting) : feedUrl);
                        SourceRepository remoteRepo = GetRemoteRepository(feedUrl);
                        UIPackageMetadata localPackage = null;
                        UIPackageMetadata repoPackage = null;

                        if (string.IsNullOrWhiteSpace(version))
                        {
                            using (tracer.Step("Version is null, search latest package by id: {0}, will not search for unlisted package.", id))
                            {
                                repoPackage = await remoteRepo.GetLatestPackageById(id);
                            }
                        }
                        else
                        {
                            using (tracer.Step("Search package by id: {0} and version: {1}, will also search for unlisted package.", id, version))
                            {
                                repoPackage = await remoteRepo.GetPackageByIdentity(id, version);
                            }
                        }

                        if (repoPackage != null)
                        {
                            using (tracer.Step("Install package: {0}.", id))
                            {
                                string installationDirectory = GetInstallationDirectory(id);
                                localPackage = await InstallExtension(repoPackage, installationDirectory, feedUrl, type, tracer);
                                siteExtensionSettings.SetValues(new KeyValuePair<string, JToken>[] {
                                    new KeyValuePair<string, JToken>(_versionSetting, localPackage.Identity.Version.ToNormalizedString()),
                                    new KeyValuePair<string, JToken>(_feedUrlSetting, feedUrl),
                                    new KeyValuePair<string, JToken>(_installUtcTimestampSetting, DateTime.UtcNow.ToString("u")),
                                    new KeyValuePair<string, JToken>(_packageType, Enum.GetName(typeof(SiteExtensionInfo.SiteExtensionType), type))
                                });
                            }
                        }

                        info = await ConvertLocalPackageToSiteExtensionInfo(localPackage, checkLatest: true, tracer: tracer);
                    }
                }
                catch (FileNotFoundException ex)
                {
                    _analytics.UnexpectedException(
                        ex,
                        method: "PUT",
                        path: string.Format(CultureInfo.InvariantCulture, "/api/siteextensions/{0}", id),
                        result: Constants.SiteExtensionProvisioningStateFailed,
                        message: string.Format(CultureInfo.InvariantCulture, "{{\"version\": {0}, \"feed_url\": {1}}}", version, feedUrl),
                        trace: false);

                    tracer.TraceError(ex);
                    info = new SiteExtensionInfo();
                    info.Id = id;
                    info.ProvisioningState = Constants.SiteExtensionProvisioningStateFailed;
                    info.Comment = string.Format(Constants.SiteExtensionProvisioningStateNotFoundMessageFormat, id);
                    status = HttpStatusCode.NotFound;
                }
                catch (WebException ex)
                {
                    _analytics.UnexpectedException(
                        ex,
                        method: "PUT",
                        path: string.Format(CultureInfo.InvariantCulture, "/api/siteextensions/{0}", id),
                        result: Constants.SiteExtensionProvisioningStateFailed,
                        message: string.Format(CultureInfo.InvariantCulture, "{{\"version\": {0}, \"feed_url\": {1}}}", version, feedUrl),
                        trace: false);

                    tracer.TraceError(ex);
                    info = new SiteExtensionInfo();
                    info.Id = id;
                    info.ProvisioningState = Constants.SiteExtensionProvisioningStateFailed;
                    info.Comment = string.Format(Constants.SiteExtensionProvisioningStateDownloadFailureMessageFormat, id);
                    status = HttpStatusCode.BadRequest;
                }
                catch (InvalidEndpointException ex)
                {
                    _analytics.UnexpectedException(ex, trace: false);

                    tracer.TraceError(ex);
                    info = new SiteExtensionInfo();
                    info.Id = id;
                    info.ProvisioningState = Constants.SiteExtensionProvisioningStateFailed;
                    info.Comment = ex.Message;
                    status = HttpStatusCode.BadRequest;
                }
                catch (Exception ex)
                {
                    _analytics.UnexpectedException(
                        ex,
                        method: "PUT",
                        path: string.Format(CultureInfo.InvariantCulture, "/api/siteextensions/{0}", id),
                        result: Constants.SiteExtensionProvisioningStateFailed,
                        message: string.Format(CultureInfo.InvariantCulture, "{{\"version\": {0}, \"feed_url\": {1}}}", version, feedUrl),
                        trace: false);

                    tracer.TraceError(ex);
                    info = new SiteExtensionInfo();
                    info.Id = id;
                    info.ProvisioningState = Constants.SiteExtensionProvisioningStateFailed;
                    info.Comment = string.Format(Constants.SiteExtensionProvisioningStateInvalidPackageMessageFormat, id);
                    status = HttpStatusCode.BadRequest;
                }
            }

            if (info == null)
            {
                // treat this as an error case since no result return from repo
                _analytics.UnexpectedException(
                        new FileNotFoundException(id),
                        method: "PUT",
                        path: string.Format(CultureInfo.InvariantCulture, "/api/siteextensions/{0}", id),
                        result: Constants.SiteExtensionProvisioningStateFailed,
                        message: string.Format(CultureInfo.InvariantCulture, "{{\"version\": {0}, \"feed_url\": {1}}}", version, feedUrl),
                        trace: false);

                info = new SiteExtensionInfo();
                info.ProvisioningState = Constants.SiteExtensionProvisioningStateFailed;
                info.Comment = string.Format(Constants.SiteExtensionProvisioningStateNotFoundMessageFormat, id);
                status = HttpStatusCode.NotFound;
            }
            else if (!string.Equals(Constants.SiteExtensionProvisioningStateFailed, info.ProvisioningState, StringComparison.OrdinalIgnoreCase))
            {
                info.ProvisioningState = Constants.SiteExtensionProvisioningStateSucceeded;
                info.Comment = null;
            }

            using (tracer.Step("Update arm settings for {0} installation. Status: {1}", id, status))
            {
                SiteExtensionStatus armSettings = new SiteExtensionStatus(_environment.SiteExtensionSettingsPath, id, tracer);
                armSettings.ReadSiteExtensionInfo(info);
                armSettings.Status = status;
                armSettings.Operation = alreadyInstalled ? null : Constants.SiteExtensionOperationInstall;
            }

            return info;
        }

        /// <summary>
        /// <para>1. Download package</para>
        /// <para>2. Generate xdt file if not exist</para>
        /// <para>3. Deploy site extension job</para>
        /// <para>4. Execute install.cmd if exist</para>
        /// </summary>
        private async Task<UIPackageMetadata> InstallExtension(UIPackageMetadata package, string installationDirectory, string feedUrl, SiteExtensionInfo.SiteExtensionType type, ITracer tracer)
        {
            try
            {
                EnsureInstallationEnviroment(installationDirectory, tracer);

                string packageLocalFilePath = GetNuGetPackageFile(package.Identity.Id, package.Identity.Version.ToString());
                bool packageExisted = FileSystemHelpers.DirectoryExists(installationDirectory);
                SourceRepository remoteRepo = GetRemoteRepository(feedUrl);

                using (tracer.Step("Download site extension: {0}", package.Identity))
                {
                    string extractPath = installationDirectory;
                    if (SiteExtensionInfo.SiteExtensionType.WebRoot == type)
                    {
                        extractPath = _environment.WebRootPath;
                        FileSystemHelpers.EnsureDirectory(extractPath);
                    }

                    // Copy/update content folder
                    // Copy/update nupkg file for package list/lookup
                    if (packageExisted)
                    {
                        await remoteRepo.UpdateLocalPackage(_localRepository, package.Identity, extractPath, packageLocalFilePath, tracer);
                    }
                    else
                    {
                        FileSystemHelpers.EnsureDirectory(installationDirectory);
                        await remoteRepo.DownloadPackageToFolder(package.Identity, extractPath, packageLocalFilePath);
                    }

                    if (SiteExtensionInfo.SiteExtensionType.WebRoot == type)
                    {
                        // if install to WebRoot, check if there is any xdt file come with package
                        // if there is one, move it to site extension folder
                        string xdtFile = Path.Combine(extractPath, Constants.ApplicationHostXdtFileName);
                        if (File.Exists(xdtFile))
                        {
                            tracer.Trace("Use xdt file from package.");
                            string newXdtFile = Path.Combine(installationDirectory, Constants.ApplicationHostXdtFileName);

                            tracer.Trace("Moving {0} to {1}", xdtFile, newXdtFile);
                            FileSystemHelpers.MoveFile(xdtFile, newXdtFile);
                        }
                        else
                        {
                            tracer.Trace("No xdt file come with package.");
                        }
                    }
                }

                // ignore below action if we install packge to wwwroot
                if (SiteExtensionInfo.SiteExtensionType.WebRoot != type)
                {
                    // If there is no xdt file, generate default.
                    using (tracer.Step("Check if applicationhost.xdt file existed."))
                    {
                        GenerateApplicationHostXdt(installationDirectory, '/' + package.Identity.Id, isPreInstalled: false, tracer: tracer);
                    }

                    using (tracer.Step("Trigger site extension job"))
                    {
                        OperationManager.Attempt(() => DeploySiteExtensionJobs(package.Identity.Id));
                    }

                    var externalCommandFactory = new ExternalCommandFactory(_environment, _settings, installationDirectory);
                    string installScript = Path.Combine(installationDirectory, _installScriptName);
                    if (FileSystemHelpers.FileExists(installScript))
                    {
                        using (tracer.Step("Execute install.cmd"))
                        {
                            OperationManager.Attempt(() =>
                            {
                                Executable exe = externalCommandFactory.BuildCommandExecutable(installScript,
                                    installationDirectory,
                                    _settings.GetCommandIdleTimeout(), NullLogger.Instance);
                                exe.ExecuteWithProgressWriter(NullLogger.Instance, _traceFactory.GetTracer(), String.Empty);
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                tracer.TraceError(ex);
                FileSystemHelpers.DeleteDirectorySafe(installationDirectory);
                throw;
            }

            return await _localRepository.GetLatestPackageById(package.Identity.Id);
        }

        /// <summary>
        /// <para>Check if there is damanged leftover data</para>
        /// <para>If there is leftover data, will try to cleanup.</para>
        /// <para>If fail to cleanup, will move leftover data to Temp folder</para>
        /// </summary>
        private static void EnsureInstallationEnviroment(string installationDir, ITracer tracer)
        {
            // folder is there but nupkg is gone, means previous uninstallation must encounter some error
            bool isInstalledPackageBroken = FileSystemHelpers.DirectoryExists(installationDir) && FileSystemHelpers.GetFiles(installationDir, "*.nupkg").Length == 0;
            if (!isInstalledPackageBroken)
            {
                return;
            }

            using (tracer.Step("There was leftover data from previous uninstallation. Trying to cleanup now."))
            {
                try
                {
                    OperationManager.Attempt(() => FileSystemHelpers.DeleteDirectorySafe(installationDir, ignoreErrors: false));
                    return;
                }
                catch (Exception ex)
                {
                    tracer.TraceError(ex);
                }

                FileSystemHelpers.EnsureDirectory(_toBeDeletedDirectoryPath);
                DirectoryInfo dirInfo = new DirectoryInfo(installationDir);
                string tmpFoder = Path.Combine(
                    _toBeDeletedDirectoryPath,
                    string.Format(CultureInfo.InvariantCulture, "{0}-{1}", dirInfo.Name, Guid.NewGuid().ToString("N").Substring(0, 8)));

                using (tracer.Step("Failed to cleanup. Moving leftover data to {0}", tmpFoder))
                {
                    // if failed, let exception bubble up to trigger bad request
                    OperationManager.Attempt(() => FileSystemHelpers.MoveDirectory(installationDir, tmpFoder));
                }
            }

            FileSystemHelpers.DeleteDirectoryContentsSafe(_toBeDeletedDirectoryPath);
        }

        /// <summary>
        /// <para> Return true if any of below cases is satisfied:</para>
        /// <para>      1) Package with same version and from same feed already exist in local repo</para>
        /// <para>      2) If given feedUrl is null</para>
        /// <para>              Try to use feed from local package, if feed from local package also null, fallback to default feed</para>
        /// <para>              Check if version from query is same as local package</para>
        /// <para>      3) If given version and feedUrl are null</para>
        /// <para>              Try to use feed from local package, if feed from local package also null, fallback to default feed</para>
        /// <para>              Check if version from query is same as local package</para>
        /// </summary>
        private async Task<bool> IsSiteExtensionInstalled(string id, string version, string feedUrl)
        {
            JsonSettings siteExtensionSettings = GetSettingManager(id);
            string localPackageVersion = siteExtensionSettings.GetValue(_versionSetting);
            string localPackageFeedUrl = siteExtensionSettings.GetValue(_feedUrlSetting);
            bool isInstalled = false;

            // Try to use given feed
            // If given feed is null, try with feed that from local package
            // And GetRemoteRepository will fallback to use default feed if pass in feed param is null
            SourceRepository remoteRepo = GetRemoteRepository(feedUrl ?? localPackageFeedUrl);

            // case 1 and 2
            if (!string.IsNullOrWhiteSpace(version)
                && version.Equals(localPackageVersion, StringComparison.OrdinalIgnoreCase)
                && remoteRepo.PackageSource.Source.Equals(localPackageFeedUrl, StringComparison.OrdinalIgnoreCase))
            {
                isInstalled = true;
            }
            else if (string.IsNullOrWhiteSpace(version) && string.IsNullOrWhiteSpace(feedUrl))
            {
                // case 3
                UIPackageMetadata remotePackage = await remoteRepo.GetLatestPackageById(id);
                if (remotePackage != null)
                {
                    isInstalled = remotePackage.Identity.Version.ToNormalizedString().Equals(localPackageVersion, StringComparison.OrdinalIgnoreCase);
                }
            }

            return isInstalled;
        }

        private SiteExtensionInfo EnablePreInstalledExtension(SiteExtensionInfo info, ITracer tracer)
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
                tracer.TraceError(ex);
                FileSystemHelpers.DeleteDirectorySafe(installationDirectory);
                return null;
            }

            return GetPreInstalledExtension(id);
        }

        private static void GenerateApplicationHostXdt(string installationDirectory, string relativeUrl, bool isPreInstalled, ITracer tracer = null)
        {
            // If there is no xdt file, generate default.
            FileSystemHelpers.CreateDirectory(installationDirectory);
            string xdtPath = Path.Combine(installationDirectory, Constants.ApplicationHostXdtFileName);
            if (!FileSystemHelpers.FileExists(xdtPath))
            {
                if (tracer != null)
                {
                    tracer.Trace("Missing xdt file, creating one.");
                }

                string physicalPath = isPreInstalled ? "%XDT_LATEST_EXTENSIONPATH%" : "%XDT_EXTENSIONPATH%";
                string xdtContent = CreateDefaultXdtFile(relativeUrl, physicalPath);
                OperationManager.Attempt(() => FileSystemHelpers.WriteAllText(xdtPath, xdtContent));
            }
        }

        public async Task<bool> UninstallExtension(string id)
        {
            ITracer tracer = _traceFactory.GetTracer();

            string installationDirectory = GetInstallationDirectory(id);

            SiteExtensionInfo info = await GetLocalExtension(id, checkLatest: false);

            if (info == null || !FileSystemHelpers.DirectoryExists(info.LocalPath))
            {
                tracer.TraceError("Site extension {0} not found.", id);
                throw new DirectoryNotFoundException(installationDirectory);
            }

            if (IsInstalledToWebRoot(id))
            {
                // special handling for package that install in wwwroot instead of under site extension folder
                tracer.Trace("Clear all content in wwwroot");
                OperationManager.Attempt(() => FileSystemHelpers.DeleteDirectoryContentsSafe(_environment.WebRootPath));
            }
            else
            {
                // default action for site extension uninstall

                // only site extension has below infomation
                var externalCommandFactory = new ExternalCommandFactory(_environment, _settings, installationDirectory);

                string uninstallScript = Path.Combine(installationDirectory, _uninstallScriptName);

                if (FileSystemHelpers.FileExists(uninstallScript))
                {
                    using (tracer.Step("Execute uninstall.cmd"))
                    {
                        OperationManager.Attempt(() =>
                        {
                            Executable exe = externalCommandFactory.BuildCommandExecutable(uninstallScript,
                                installationDirectory,
                                _settings.GetCommandIdleTimeout(), NullLogger.Instance);
                            exe.ExecuteWithProgressWriter(NullLogger.Instance, _traceFactory.GetTracer(), String.Empty);
                        });
                    }
                }

                using (tracer.Step("Remove site extension job"))
                {
                    OperationManager.Attempt(() => CleanupSiteExtensionJobs(id));
                }
            }

            using (tracer.Step("Delete site extension package, directory and arm settings"))
            {
                OperationManager.Attempt(() => FileSystemHelpers.DeleteFileSafe(GetNuGetPackageFile(info.Id, info.Version)));
                OperationManager.Attempt(() => FileSystemHelpers.DeleteDirectorySafe(installationDirectory));
                SiteExtensionStatus armSettings = new SiteExtensionStatus(_environment.SiteExtensionSettingsPath, id, tracer);
                await armSettings.RemoveStatus();
            }

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
            return Path.Combine(GetInstallationDirectory(id), String.Format(CultureInfo.InvariantCulture, "{0}.{1}.nupkg", id, version));
        }

        private static string GetPreInstalledDirectory(string id)
        {
            string programFiles = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86);
            return Path.Combine(programFiles, "SiteExtensions", id);
        }

        private static string CreateDefaultXdtFile(string relativeUrl, string physicalPath)
        {
            string template = null;

            Stream stream = typeof(SiteExtensionManager).Assembly.GetManifestResourceStream("Kudu.Core.SiteExtensions." + Constants.ApplicationHostXdtFileName + ".xml");
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
                info.ExtensionUrl = FileSystemHelpers.FileExists(Path.Combine(localPath, Constants.ApplicationHostXdtFileName))
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
                appHostDoc.Load(Path.Combine(info.LocalPath, Constants.ApplicationHostXdtFileName));

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

        private async Task<SiteExtensionInfo> ConvertLocalPackageToSiteExtensionInfo(UIPackageMetadata package, bool checkLatest, ITracer tracer = null)
        {
            if (package == null)
            {
                return null;
            }

            var info = new SiteExtensionInfo(package);
            if (IsInstalledToWebRoot(info.Id))
            {
                info.Type = SiteExtensionInfo.SiteExtensionType.WebRoot;
            }

            SetLocalInfo(info);
            await TryCheckLocalPackageLatestVersionFromRemote(info, checkLatest, tracer);
            return info;
        }

        private async Task TryCheckLocalPackageLatestVersionFromRemote(SiteExtensionInfo info, bool checkLatest, ITracer tracer = null)
        {
            if (checkLatest)
            {
                try
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
                catch (Exception ex)
                {
                    if (tracer != null)
                    {
                        tracer.TraceError(ex);
                    }
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

        private bool IsInstalledToWebRoot(string packageId)
        {
            JsonSettings siteExtensionSettings = GetSettingManager(packageId);
            string packageTypeStr = siteExtensionSettings.GetValue(_packageType);
            SiteExtensionInfo.SiteExtensionType packageType;

            return (Enum.TryParse<SiteExtensionInfo.SiteExtensionType>(packageTypeStr, true, out packageType)
                && SiteExtensionInfo.SiteExtensionType.WebRoot == packageType);
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

        private async Task<List<SiteExtensionInfo>> ConvertNuGetPackagesToSiteExtensionInfos(
            IEnumerable<UIPackageMetadata> packages,
            Func<UIPackageMetadata, Task<SiteExtensionInfo>> convertor)
        {
            List<SiteExtensionInfo> siteExtensionInfos = new List<SiteExtensionInfo>();
            List<Task<SiteExtensionInfo>> convertTasks = new List<Task<SiteExtensionInfo>>();

            // in case there are a large amount of package come back from search result, we limited number of concurrent requests to be the number of CPU cores
            SemaphoreSlim throttler = new SemaphoreSlim(System.Environment.ProcessorCount);

            foreach (var item in packages)
            {
                convertTasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await throttler.WaitAsync();
                        return await convertor(item);
                    }
                    finally
                    {
                        throttler.Release();
                    }
                }));
            }

            await Task.WhenAll(convertTasks);
            foreach (var item in convertTasks)
            {
                siteExtensionInfos.Add(item.Result);
            }

            return siteExtensionInfos;
        }
    }
}
