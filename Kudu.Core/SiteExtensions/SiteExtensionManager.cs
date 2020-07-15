using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
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
using Kudu.Core.Helpers;
using Kudu.Core.Infrastructure;
using Kudu.Core.Settings;
using Kudu.Core.Tracing;
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
        private const string _installationArgs = "installer_command_line_params";

        private readonly string _rootPath;
        private readonly string _baseUrl;
        private readonly IContinuousJobsManager _continuousJobManager;
        private readonly ITriggeredJobsManager _triggeredJobManager;

        private static readonly string _toBeDeletedDirectoryPath = System.Environment.ExpandEnvironmentVariables(@"%HOME%\data\Temp\SiteExtensions");


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

        // This is for mock testing only
        public static string BinDirectory
        {
            get;
            set;
        }

        public async Task<IEnumerable<SiteExtensionInfo>> GetRemoteExtensions(string filter, bool allowPrereleaseVersions, string feedUrl)
        {
            ITracer tracer = _traceFactory.GetTracer();
            var extensions = new List<SiteExtensionInfo>();
            IEnumerable<SourceRepository> remoteRepos = GetRemoteRepositories(feedUrl);

            SearchFilter filterOptions = new SearchFilter();
            filterOptions.IncludePrerelease = allowPrereleaseVersions;

            IEnumerable<UIPackageMetadata> packages = new List<UIPackageMetadata>();

            using (tracer.Step("Search site extensions by filter: {0}", filter))
            {
                foreach (SourceRepository remoteRepo in remoteRepos)
                {
                    var foundUIPackages = await remoteRepo.Search(string.IsNullOrWhiteSpace(filter) ? string.Empty : filter, filterOptions: filterOptions);
                    if (null != foundUIPackages && foundUIPackages.Count() > 0)
                    {
                        packages = packages.Concat(foundUIPackages);
                    }
                }
                packages = packages.OrderByDescending(p => p.DownloadCount);
            }

            using (tracer.Step("Convert search result to SiteExtensionInfos with max concurrent requests: {0}", System.Environment.ProcessorCount))
            {
                // GetResourceAsync is not thread safe
                var metadataResource = await _localRepository.GetResourceAndValidateAsync<UIMetadataResource>();
                var convertedResult = await ConvertNuGetPackagesToSiteExtensionInfos(
                    packages,
                    async (uiPackage) =>
                    {
                        // this function is called in multiple threads simultaneously
                        return await ConvertRemotePackageToSiteExtensionInfo(uiPackage, feedUrl, metadataResource);
                    });

                extensions.AddRange(convertedResult);
            }

            return extensions;
        }

        public async Task<SiteExtensionInfo> GetRemoteExtension(string id, string version, string feedUrl)
        {
            ITracer tracer = _traceFactory.GetTracer();

            IEnumerable<SourceRepository> remoteRepos = GetRemoteRepositories(feedUrl);
            UIPackageMetadata package = null;

            if (string.IsNullOrWhiteSpace(version))
            {
                using (tracer.Step("Version is null, search latest package by id: {0}", id))
                {
                    foreach (SourceRepository remoteRepo in remoteRepos)
                    {
                        package = await remoteRepo.GetLatestPackageByIdFromSrcRepo(id);
                        if (package != null)
                        {
                            break;
                        }
                    }
                }
            }
            else
            {
                using (tracer.Step("Search package by id: {0} and version: {1}", id, version))
                {
                    foreach (SourceRepository remoteRepo in remoteRepos)
                    {
                        package = await remoteRepo.GetPackageByIdentity(id, version);
                        if (package != null)
                        {
                            break;
                        }
                    }
                }
            }

            if (package == null)
            {
                tracer.Trace("No package found with id: {0} and version: {1}", id, version);
                return null;
            }

            var metadataResource = await _localRepository.GetResourceAndValidateAsync<UIMetadataResource>();
            return await ConvertRemotePackageToSiteExtensionInfo(package, feedUrl, metadataResource);
        }

        public async Task<IEnumerable<SiteExtensionInfo>> GetLocalExtensions(string filter, bool checkLatest)
        {
            ITracer tracer = _traceFactory.GetTracer();
            IEnumerable<UIPackageMetadata> searchResult = null;
            List<SiteExtensionInfo> siteExtensionInfos = new List<SiteExtensionInfo>();

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

            return siteExtensionInfos;
        }

        // <inheritdoc />
        public async Task<SiteExtensionInfo> GetLocalExtension(string id, bool checkLatest = true)
        {
            ITracer tracer = _traceFactory.GetTracer();
            UIPackageMetadata package = null;
            using (tracer.Step("Now querying from local repo for package '{0}'.", id))
            {
                package = await _localRepository.GetLatestPackageByIdFromSrcRepo(id);
            }

            if (package == null)
            {
                tracer.Trace("No package found from local repo with id: {0}.", id);
                return null;
            }

            SiteExtensionInfo info;
            using (tracer.Step("Converting NuGet object to SiteExtensionInfo"))
            {
                info = await ConvertLocalPackageToSiteExtensionInfo(package, checkLatest, tracer: tracer);
            }

            SiteExtensionStatus armSettings = new SiteExtensionStatus(_environment.SiteExtensionSettingsPath, id, tracer);
            armSettings.FillSiteExtensionInfo(info);
            return info;
        }

        public Task<SiteExtensionInfo> InstallExtension(string id, SiteExtensionInfo requestInfo, ITracer tracer)
        {
            var installationTask = InstallExtensionCore(id, requestInfo, tracer);

#pragma warning disable 4014
            // Track pending task
            PostDeploymentHelper.TrackPendingOperation(installationTask, TimeSpan.Zero);
#pragma warning restore 4014

            return installationTask;
        }

        private async Task<SiteExtensionInfo> InstallExtensionCore(string id, SiteExtensionInfo requestInfo, ITracer tracer)
        {
            var version = requestInfo.Version;
            var feedUrl = requestInfo.FeedUrl;
            var type = requestInfo.Type;
            var installationArgs = requestInfo.InstallationArgs;
            try
            {
                using (tracer.Step("Installing '{0}' version '{1}' from '{2}'", id, version, feedUrl))
                {
                    var installationLock = SiteExtensionInstallationLock.CreateLock(_environment.SiteExtensionSettingsPath, id, enableAsync: true);
                    // hold on to lock till action complete (success or fail)
                    return await installationLock.LockOperationAsync<SiteExtensionInfo>(async () =>
                    {
                        return await TryInstallExtension(id, version, feedUrl, type, tracer, installationArgs);
                    }, "Installing SiteExtension", TimeSpan.Zero);
                }
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

        private async Task<SiteExtensionInfo> TryInstallExtension(string id, string version, string feedUrl, SiteExtensionInfo.SiteExtensionType type, ITracer tracer, string installationArgs)
        {
            SiteExtensionInfo info = null;
            HttpStatusCode status = HttpStatusCode.OK;  // final status when success
            bool alreadyInstalled = false;

            try
            {
                // Check if site extension already installed (id, version, feedUrl), if already install with correct installation arguments then return right away
                if (await this.IsSiteExtensionInstalled(id, version, feedUrl, installationArgs))
                {
                    // package already installed, return package from local repo.
                    tracer.Trace("Package {0} with version {1} from {2} with installation arguments '{3}' already installed.", id, version, feedUrl, installationArgs);
                    info = await GetLocalExtension(id);
                    alreadyInstalled = info != null;
                }

                if (!alreadyInstalled)
                {
                    JsonSettings siteExtensionSettings = GetSettingManager(id);
                    feedUrl = (string.IsNullOrEmpty(feedUrl) ? siteExtensionSettings.GetValue(_feedUrlSetting) : feedUrl);
                    IEnumerable<SourceRepository> remoteRepos = GetRemoteRepositories(feedUrl);
                    UIPackageMetadata localPackage = null;
                    UIPackageMetadata repoPackage = null;
                    SourceRepository remoteRepo = null;

                    if (this.IsInstalledToWebRoot(id))
                    {
                        // override WebRoot type from setting
                        // WebRoot is a special type that install package to wwwroot, when perform update we need to update new content to wwwroot even if type is not specified
                        type = SiteExtensionInfo.SiteExtensionType.WebRoot;
                    }

                    if (string.IsNullOrWhiteSpace(version))
                    {
                        using (tracer.Step("Version is null, search latest package by id: {0}, will not search for unlisted package.", id))
                        {
                            foreach (SourceRepository rr in remoteRepos)
                            {
                                repoPackage = await rr.GetLatestPackageByIdFromSrcRepo(id);
                                if (repoPackage != null)
                                {
                                    remoteRepo = rr;
                                    break;
                                }
                            }
                        }
                    }
                    else
                    {
                        using (tracer.Step("Search package by id: {0} and version: {1}, will also search for unlisted package.", id, version))
                        {
                            foreach (SourceRepository rr in remoteRepos)
                            {
                                repoPackage = await rr.GetPackageByIdentity(id, version);
                                if (repoPackage != null)
                                {
                                    remoteRepo = rr;
                                    break;
                                }
                            }
                        }
                    }

                    if (repoPackage != null)
                    {
                        Debug.Assert(remoteRepo != null, "remote SourceRepository should not be null!");
                        using (tracer.Step("Install package: {0}.", id))
                        {
                            string installationDirectory = GetInstallationDirectory(id);
                            localPackage = await InstallExtension(repoPackage, installationDirectory, remoteRepo, type, tracer, installationArgs);
                            siteExtensionSettings.SetValues(new KeyValuePair<string, JToken>[] {
                                new KeyValuePair<string, JToken>(_versionSetting, localPackage.Identity.Version.ToNormalizedString()),
                                new KeyValuePair<string, JToken>(_feedUrlSetting, feedUrl),
                                new KeyValuePair<string, JToken>(_installUtcTimestampSetting, DateTime.UtcNow.ToString("u")),
                                new KeyValuePair<string, JToken>(_packageType, Enum.GetName(typeof(SiteExtensionInfo.SiteExtensionType), type)),
                                new KeyValuePair<string, JToken>(_installationArgs, installationArgs)
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
                info.Comment = ex.ToString();
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
                info.Comment = ex.ToString();
                status = HttpStatusCode.BadRequest;
            }
            catch (InvalidEndpointException ex)
            {
                _analytics.UnexpectedException(ex, trace: false);

                tracer.TraceError(ex);
                info = new SiteExtensionInfo();
                info.Id = id;
                info.ProvisioningState = Constants.SiteExtensionProvisioningStateFailed;
                info.Comment = ex.ToString();
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
                info.Comment = ex.ToString();
                status = HttpStatusCode.BadRequest;
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
        private async Task<UIPackageMetadata> InstallExtension(UIPackageMetadata package, string installationDirectory, SourceRepository remoteRepo, SiteExtensionInfo.SiteExtensionType type, ITracer tracer, string installationArgs)
        {
            try
            {
                EnsureInstallationEnvironment(installationDirectory, tracer);

                string packageLocalFilePath = GetNuGetPackageFile(package.Identity.Id, package.Identity.Version.ToNormalizedString());
                bool packageExisted = FileSystemHelpers.DirectoryExists(installationDirectory);

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
                        // if install to WebRoot, check if there is any xdt or scmXdt file come with package
                        // if there is, move it to site extension folder
                        string xdtFile = Path.Combine(extractPath, Constants.ApplicationHostXdtFileName);
                        string scmXdtFile = Path.Combine(extractPath, Constants.ScmApplicationHostXdtFileName);
                        bool findXdts = false;

                        if (FileSystemHelpers.FileExists(xdtFile))
                        {
                            tracer.Trace("Use xdt file from package.");
                            string newXdtFile = Path.Combine(installationDirectory, Constants.ApplicationHostXdtFileName);

                            tracer.Trace("Moving {0} to {1}", xdtFile, newXdtFile);
                            FileSystemHelpers.MoveFile(xdtFile, newXdtFile);
                            findXdts = true;
                        }
                        // if the siteextension applies to both main site and the scm site
                        if (FileSystemHelpers.FileExists(scmXdtFile))
                        {
                            tracer.Trace("Use scmXdt file from package.");
                            string newScmXdtFile = Path.Combine(installationDirectory, Constants.ScmApplicationHostXdtFileName);

                            tracer.Trace("Moving {0} to {1}", scmXdtFile, newScmXdtFile);
                            FileSystemHelpers.MoveFile(scmXdtFile, newScmXdtFile);
                            findXdts = true;
                        }

                        if (!findXdts)
                        {
                            tracer.Trace("No xdt file come with package.");
                        }
                    }
                }

                // ignore below action if we install package to wwwroot
                if (SiteExtensionInfo.SiteExtensionType.WebRoot != type)
                {
                    using (tracer.Step("Check if applicationHost.xdt or scmApplicationHost.xdt file existed."))
                    {
                        GenerateDefaultScmApplicationHostXdt(installationDirectory, '/' + package.Identity.Id, tracer: tracer);
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
                                exe.ExecuteWithProgressWriter(NullLogger.Instance, _traceFactory.GetTracer(), installationArgs ?? String.Empty);
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

            return await _localRepository.GetLatestPackageByIdFromSrcRepo(package.Identity.Id);
        }

        /// <summary>
        /// <para>Check if there is damaged leftover data</para>
        /// <para>If there is leftover data, will try to cleanup.</para>
        /// <para>If fail to cleanup, will move leftover data to Temp folder</para>
        /// </summary>
        private static void EnsureInstallationEnvironment(string installationDir, ITracer tracer)
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
                string tmpFolder = Path.Combine(
                    _toBeDeletedDirectoryPath,
                    string.Format(CultureInfo.InvariantCulture, "{0}-{1}", dirInfo.Name, Guid.NewGuid().ToString("N").Substring(0, 8)));

                using (tracer.Step("Failed to cleanup. Moving leftover data to {0}", tmpFolder))
                {
                    // if failed, let exception bubble up to trigger bad request
                    OperationManager.Attempt(() => FileSystemHelpers.MoveDirectory(installationDir, tmpFolder));
                }
            }

            FileSystemHelpers.DeleteDirectoryContentsSafe(_toBeDeletedDirectoryPath);
        }

        /// <summary>
        /// <para> Return false if the installation arguments are different. Otherwise,</para>
        /// <para> Return true if any of below cases is satisfied:</para>
        /// <para>      1) Package with same version and from same feed already exist in local repo</para>
        /// <para>      2) If given feedUrl is null</para>
        /// <para>              Try to use feed from local package, if feed from local package also null, fallback to default feed</para>
        /// <para>              Check if version from query is same as local package</para>
        /// <para>      3) If given version and feedUrl are null</para>
        /// <para>              Try to use feed from local package, if feed from local package also null, fallback to default feed</para>
        /// <para>              Check if version from query is same as local package</para>
        /// </summary>
        private async Task<bool> IsSiteExtensionInstalled(string id, string version, string feedUrl, string installationArgs)
        {
            ITracer tracer = _traceFactory.GetTracer();
            JsonSettings siteExtensionSettings = GetSettingManager(id);
            string localPackageVersion = siteExtensionSettings.GetValue(_versionSetting);
            string localPackageFeedUrl = siteExtensionSettings.GetValue(_feedUrlSetting);
            string localPackageInstallationArgs = siteExtensionSettings.GetValue(_installationArgs);
            NuGetVersion localPkgVer = null;
            NuGetVersion lastFoundVer = null;
            bool isInstalled = false;

            // Shortcircuit check: if the installation arguments do not match, then we should return false here to avoid other checks which are now unnecessary.
            if (!string.Equals(localPackageInstallationArgs, installationArgs))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(localPackageVersion))
            {
                localPkgVer = NuGetVersion.Parse(localPackageVersion);
            }

            // Try to use given feed
            // If given feed is null, try with feed that from local package
            // And GetRemoteRepositories will fallback to use default feed if pass in feed param is null
            IEnumerable<SourceRepository> remoteRepos = GetRemoteRepositories(feedUrl ?? localPackageFeedUrl);

            foreach (SourceRepository rr in remoteRepos)
            {
                // case 1 and 2
                if (!string.IsNullOrWhiteSpace(version)
                    && version.Equals(localPackageVersion, StringComparison.OrdinalIgnoreCase)
                    && rr.PackageSource.Source.Equals(localPackageFeedUrl, StringComparison.OrdinalIgnoreCase))
                {
                    isInstalled = true;
                }
                else if (string.IsNullOrWhiteSpace(version)
                         && string.IsNullOrWhiteSpace(feedUrl)
                         && string.Equals(localPackageInstallationArgs, installationArgs))
                {
                    // case 3
                    UIPackageMetadata remotePackage = await rr.GetLatestPackageByIdFromSrcRepo(id);
                    if (remotePackage != null)
                    {
                        tracer.Trace("Found version: {0} on feed: {1}",
                                     remotePackage.Identity.Version.ToNormalizedString(),
                                     rr.PackageSource.Source);
                        if (lastFoundVer == null || lastFoundVer < remotePackage.Identity.Version)
                        {
                            lastFoundVer = remotePackage.Identity.Version;
                        }
                    }
                }
            }

            if (lastFoundVer != null && localPkgVer != null && lastFoundVer <= localPkgVer)
            {
                isInstalled = true;
            }

            return isInstalled;
        }

        private static void GenerateDefaultScmApplicationHostXdt(string installationDirectory, string relativeUrl, ITracer tracer = null)
        {
            // If there is no xdt and scmXdt file, generate default scmXdt file.
            FileSystemHelpers.CreateDirectory(installationDirectory);
            string xdtPath = Path.Combine(installationDirectory, Constants.ApplicationHostXdtFileName);
            string scmXdtPath = Path.Combine(installationDirectory, Constants.ScmApplicationHostXdtFileName);

            if (!FileSystemHelpers.FileExists(xdtPath) && !FileSystemHelpers.FileExists(scmXdtPath))
            {
                if (tracer != null)
                {
                    tracer.Trace("Missing xdt file, creating one.");
                }

                string xdtContent = CreateDefaultScmXdtFile(relativeUrl, "%XDT_EXTENSIONPATH%");
                OperationManager.Attempt(() => FileSystemHelpers.WriteAllText(scmXdtPath, xdtContent));
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

        private IEnumerable<SourceRepository> GetRemoteRepositories(string feedUrl)
        {
            List<SourceRepository> repos = new List<SourceRepository>();

            if (!string.IsNullOrWhiteSpace(feedUrl))
            {
                repos.Add(GetSourceRepository(feedUrl));
            }
            else
            {
                string remoteUrl = _settings.GetSiteExtensionRemoteUrl(out _);
                repos.Add(GetSourceRepository(remoteUrl));
            }
            return repos;
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

        private static string CreateDefaultScmXdtFile(string relativeUrl, string physicalPath)
        {
            string template = null;
            // the template has "%XDT_SCMSITENAME%" by default
            Stream stream = typeof(SiteExtensionManager).Assembly.GetManifestResourceStream("Kudu.Core.SiteExtensions." + Constants.ScmApplicationHostXdtFileName + ".xml");
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
                info.ExtensionUrl = GetFullUrl(GetUrlFromApplicationHost(localPath));
            }
            else
            {
                info.ExtensionUrl = String.IsNullOrEmpty(info.LocalPath) ? null : GetFullUrl(info.ExtensionUrl);
            }

            foreach (var setting in GetSettingManager(info.Id).GetValues())
            {
                if (String.Equals(setting.Key, _feedUrlSetting, StringComparison.OrdinalIgnoreCase))
                {
                    info.FeedUrl = setting.Value.Value<string>();
                }
                else if (String.Equals(setting.Key, _installUtcTimestampSetting, StringComparison.OrdinalIgnoreCase))
                {
                    DateTime installedDateTime;
                    if (DateTime.TryParse(setting.Value.Value<string>(), out installedDateTime))
                    {
                        info.InstalledDateTime = installedDateTime.ToUniversalTime();
                    }
                }
                else if (String.Equals(setting.Key, _installationArgs, StringComparison.OrdinalIgnoreCase))
                {
                    info.InstallationArgs = setting.Value.Value<string>();
                }
            }
        }

        private static string GetUrlFromApplicationHost(string localPath)
        {
            string xdtFile = Path.Combine(localPath, Constants.ApplicationHostXdtFileName);
            string scmXdtFile = Path.Combine(localPath, Constants.ScmApplicationHostXdtFileName);
            string xdtFilePath = FileSystemHelpers.FileExists(scmXdtFile) ? scmXdtFile : FileSystemHelpers.FileExists(xdtFile) ? xdtFile : null;

            if (xdtFilePath != null)
            {
                try
                {
                    var appHostDoc = new XmlDocument();

                    appHostDoc.Load(xdtFilePath);
                    // yet there's no siteextension with both applicationHost.xdt
                    // Get the 'path' property of the first 'application' element, which is the relative url.
                    XmlNode pathPropertyNode = appHostDoc.SelectSingleNode("//application[@path]/@path");

                    return pathPropertyNode.Value;

                }
                catch (SystemException)
                {
                    // return null
                }
            }
            return null;

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

        private async Task<SiteExtensionInfo> ConvertRemotePackageToSiteExtensionInfo(UIPackageMetadata package, string feedUrl, UIMetadataResource metadataResource)
        {
            // convert uipackagemetadata structure to siteextensioninfo structure
            var siteExtensionInfo = new SiteExtensionInfo(package);
            siteExtensionInfo.FeedUrl = feedUrl;
            // check existing local site extension version and update the field "LocalIsLatestVersion" in siteextensioninfo
            return await CheckRemotePackageLatestVersion(siteExtensionInfo, metadataResource);
        }

        private async Task<SiteExtensionInfo> CheckRemotePackageLatestVersion(SiteExtensionInfo info, UIMetadataResource metadataResource)
        {
            bool isNuGetPackage = false;

            if (!string.IsNullOrEmpty(info.FeedUrl))
            {
                isNuGetPackage = FeedExtensions.IsNuGetRepo(info.FeedUrl);
            }

            UIPackageMetadata localPackage = await metadataResource.GetLatestPackageByIdFromMetaRes(info.Id,
                explicitTag: isNuGetPackage);

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
                    IEnumerable<SourceRepository> remoteRepos = GetRemoteRepositories(info.FeedUrl);
                    foreach (SourceRepository remoteRepo in remoteRepos)
                    {
                        UIPackageMetadata latestPackage = await remoteRepo.GetLatestPackageByIdFromSrcRepo(info.Id);
                        if (latestPackage != null)
                        {
                            NuGetVersion currentVersion = NuGetVersion.Parse(info.Version);
                            info.LocalIsLatestVersion = NuGetVersion.Parse(info.Version).Equals(latestPackage.Identity.Version);
                            info.DownloadCount = latestPackage.DownloadCount;
                            info.PublishedDateTime = latestPackage.Published;
                            break;
                        }
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
            return ConfigurationManager.AppSettings[appSettingName] != "beta";
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
        public static SourceRepository GetSourceRepository(string feedEndpoint)
        {
            NuGet.Configuration.PackageSource source = new NuGet.Configuration.PackageSource(feedEndpoint);
            return new SourceRepository(source, _providers.Value);
        }

        private static IEnumerable<Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>> GetNugetProviders()
        {
            var aggregateCatalog = new AggregateCatalog();
            var path = BinDirectory;
            if (string.IsNullOrEmpty(path))
            {
                path = HttpRuntime.BinDirectory;
            }
            var directoryCatalog = new DirectoryCatalog(path, "NuGet.Client*.dll");
            aggregateCatalog.Catalogs.Add(directoryCatalog);
            var container = new CompositionContainer(aggregateCatalog);
            return container.GetExports<INuGetResourceProvider, INuGetResourceProviderMetadata>();
        }

        private static async Task<List<SiteExtensionInfo>> ConvertNuGetPackagesToSiteExtensionInfos(
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
