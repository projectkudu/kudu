using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
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
using NuGet.Client.VisualStudio;
using NullLogger = Kudu.Core.Deployment.NullLogger;

namespace Kudu.Core.SiteExtensions
{
    public class SiteExtensionManagerV2 : ISiteExtensionManager
    {
        private readonly IEnvironment _environment;
        private readonly IDeploymentSettingsManager _settings;
        private readonly ITraceFactory _traceFactory;
        private readonly IAnalytics _analytics;

        private const string _settingsFileName = "SiteExtensionSettings.json";
        private const string _feedUrlSetting = "feed_url";
        private const string _packageUriSetting = "packageUri";
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

        public SiteExtensionManagerV2(IContinuousJobsManager continuousJobManager, ITriggeredJobsManager triggeredJobManager, IEnvironment environment, IDeploymentSettingsManager settings, ITraceFactory traceFactory, HttpContextBase context, IAnalytics analytics)
        {
            _rootPath = Path.Combine(environment.RootPath, "SiteExtensions");
            _baseUrl = context.Request.Url == null ? String.Empty : context.Request.Url.GetLeftPart(UriPartial.Authority).TrimEnd('/');
            _continuousJobManager = continuousJobManager;
            _triggeredJobManager = triggeredJobManager;
            _environment = environment;
            _settings = settings;
            _traceFactory = traceFactory;
            _analytics = analytics;

            FeedExtensionsV2.HttpClientTimeout = _settings.GetHttpClientTimeout();
        }

        /// <summary>
        /// Gets Remote extensions directly from NuGet V2 API 
        /// </summary>
        /// <param name="filter">query filter term inputted as a query param to the API to filter on tags/title</param>
        /// <param name="allowPrereleaseVersions">boolean flag to determine if results should include preview versions</param>
        /// <param name="feedUrl">NuGet feed URL</param>
        /// <returns></returns>
        public async Task<IEnumerable<SiteExtensionInfo>> GetRemoteExtensions(string filter, bool allowPrereleaseVersions, string feedUrl)
        {
            ITracer tracer = _traceFactory.GetTracer();
            var extensions = new List<SiteExtensionInfo>();

            using (tracer.Step("Search site extensions directly from nuget api by filter: {0}", filter))
            {
                extensions = await FeedExtensionsV2.PackageCacheInfo.GetPackagesFromNugetAPI(filter, feedUrl);
            }

            foreach (var ext in extensions)
            {
                await SetLocalInfo(ext);
            }

            return extensions;
        }

        /// <summary>
        /// Returns a remote site extension metadata for a particular id and version, null 
        /// if this id and version doesn't exist in the report repo
        /// </summary>
        public async Task<SiteExtensionInfo> GetRemoteExtension(string id, string version, string feedUrl)
        {
            ITracer tracer = _traceFactory.GetTracer();
            SiteExtensionInfo package = await GetSiteExtensionInfoFromRemote(id, version, tracer);

            if (package == null)
            {
                tracer.Trace("No package found with id: {0} and version: {1}", id, version);
                return null;
            }

            if (package != null)
            {
                await SetLocalInfo(package);
            }

            return package;
        }


        /// <summary>
        /// Gets and returns metadata for the locally installed site extension
        /// </summary>
        /// <param name="filter"></param>
        /// <param name="checkLatest"></param>
        /// <returns></returns>
        public async Task<IEnumerable<SiteExtensionInfo>> GetLocalExtensions(string filter, bool checkLatest = true)
        {
            ITracer tracer = _traceFactory.GetTracer();
            IEnumerable<SiteExtensionInfo> siteExtensionInfos;
            using (tracer.Step("Search packages locally with filter: {0}", filter))
            {
                siteExtensionInfos = await FeedExtensionsV2.SearchLocalRepo(_rootPath, filter);
            }

            using (tracer.Step("Adding local metadata to site extensions"))
            {
                foreach (var ext in siteExtensionInfos)
                {
                    await SetLocalInfo(ext);
                    SiteExtensionStatus armSettings = new SiteExtensionStatus(_environment.SiteExtensionSettingsPath, ext.Id, tracer);
                    armSettings.FillSiteExtensionInfo(ext, defaultProvisionState: Constants.SiteExtensionProvisioningStateSucceeded);
                }
            }

            return siteExtensionInfos;
        }

        // <inheritdoc />
        public async Task<SiteExtensionInfo> GetLocalExtension(string id, bool checkLatest = true)
        {
            SiteExtensionInfo info;
            ITracer tracer = _traceFactory.GetTracer();

            using (tracer.Step("Now querying from local repo for package '{0}'.", id))
            {
                info = (await GetLocalExtensions(id)).FirstOrDefault();
            }

            if (info == null)
            {
                tracer.Trace("No package found from local repo with id: {0}.", id);
                return null;
            }

            SiteExtensionStatus armSettings = new SiteExtensionStatus(_environment.SiteExtensionSettingsPath, id, tracer);
            armSettings.FillSiteExtensionInfo(info);
            return info;
        }

        /// <summary>
        /// Installs a SiteExtension a particular id and version to the site's mounted dir.
        /// If the version is null, gets the latest version from the feed url
        /// </summary>
        /// <returns></returns>
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
            var packageUri = requestInfo.PackageUri;
            try
            {
                using (tracer.Step("Installing '{0}' version '{1}' from '{2}'", id, version, StringUtils.ObfuscatePath(!string.IsNullOrEmpty(packageUri) ? packageUri : feedUrl)))
                {
                    var installationLock = SiteExtensionInstallationLock.CreateLock(_environment.SiteExtensionSettingsPath, id, enableAsync: true);
                    // hold on to lock till action complete (success or fail)
                    return await installationLock.LockOperationAsync<SiteExtensionInfo>(async () =>
                    {
                        return await TryInstallExtension(id, requestInfo, tracer);
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

        private async Task<SiteExtensionInfo> TryInstallExtension(string id, SiteExtensionInfo requestInfo, ITracer tracer)
        {
            SiteExtensionInfo info = null;
            HttpStatusCode status = HttpStatusCode.OK;  // final status when success
            bool alreadyInstalled = false;
            var version = requestInfo.Version;
            var feedUrl = requestInfo.FeedUrl;
            var type = requestInfo.Type;
            var installationArgs = requestInfo.InstallationArgs;
            var packageUri = requestInfo.PackageUri;

            try
            {
                // Check if site extension already installed (id, version, feedUrl), if already install with correct installation arguments then return right away
                if (await IsSiteExtensionInstalled(id, requestInfo))
                {
                    // package already installed, return package from local repo.
                    tracer.Trace("Package {0} with version {1} from {2} with installation arguments '{3}' already installed.", id, version, feedUrl, installationArgs);
                    info = await GetLocalExtension(id);
                    alreadyInstalled = true;
                }
                else
                {
                    JsonSettings siteExtensionSettings = GetSettingManager(id);
                    feedUrl = (string.IsNullOrEmpty(feedUrl) ? siteExtensionSettings.GetValue(_feedUrlSetting) : feedUrl);
                    info = !string.IsNullOrEmpty(packageUri) ? requestInfo : await GetSiteExtensionInfoFromRemote(id, version, tracer);

                    if (info != null)
                    {
                        using (tracer.Step("Install package: {0}.", id))
                        {
                            string installationDirectory = GetInstallationDirectory(id);
                            info = await InstallExtension(info, installationDirectory, type, tracer, installationArgs);
                            siteExtensionSettings.SetValues(new KeyValuePair<string, JToken>[]
                            {
                                new KeyValuePair<string, JToken>(_versionSetting, info.Version),
                                new KeyValuePair<string, JToken>(_feedUrlSetting, feedUrl),
                                new KeyValuePair<string, JToken>(_packageUriSetting, packageUri),
                                new KeyValuePair<string, JToken>(_installUtcTimestampSetting, DateTime.UtcNow.ToString("u")),
                                new KeyValuePair<string, JToken>(_packageType, Enum.GetName(typeof(SiteExtensionInfo.SiteExtensionType), type)),
                                new KeyValuePair<string, JToken>(_installationArgs, installationArgs)
                            });
                        }
                    }
                }

                if (info != null)
                {
                    await SetLocalInfo(info);
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
        private async Task<SiteExtensionInfo> InstallExtension(SiteExtensionInfo package, string installationDirectory, SiteExtensionInfo.SiteExtensionType type, ITracer tracer, string installationArgs)
        {
            try
            {
                EnsureInstallationEnvironment(installationDirectory, tracer);

                string packageLocalFilePath = GetNuGetPackageFile(package.Id, package.Version);
                bool packageExisted = FileSystemHelpers.DirectoryExists(installationDirectory);

                using (tracer.Step("Download site extension: {0}", package.Id))
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
                        await FeedExtensionsV2.UpdateLocalPackage(_rootPath, package, extractPath, packageLocalFilePath, tracer);
                    }
                    else
                    {
                        FileSystemHelpers.EnsureDirectory(installationDirectory);
                        await FeedExtensionsV2.DownloadPackageToFolder(package, extractPath, packageLocalFilePath);
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
                        GenerateDefaultScmApplicationHostXdt(installationDirectory, '/' + package.Id, tracer: tracer);
                    }

                    using (tracer.Step("Trigger site extension job"))
                    {
                        OperationManager.Attempt(() => DeploySiteExtensionJobs(package.Id));
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

            var result = (await FeedExtensionsV2.SearchLocalRepo(_rootPath, package.Id)).FirstOrDefault();
            if (result != null && !string.IsNullOrEmpty(package.PackageUri))
            {
                result.PackageUri = package.PackageUri;
            }
            return result;
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
        private async Task<bool> IsSiteExtensionInstalled(string id, SiteExtensionInfo requestInfo)
        {
            ITracer tracer = _traceFactory.GetTracer();
            JsonSettings siteExtensionSettings = GetSettingManager(id);
            string localPackageVersion = siteExtensionSettings.GetValue(_versionSetting);
            string localPackageInstallationArgs = siteExtensionSettings.GetValue(_installationArgs);
            SemanticVersion localPkgVer = null;
            SemanticVersion lastFoundVer = null;
            SiteExtensionInfo latestRemotePackage;
            bool isInstalled = false;
            var installationArgs = requestInfo.InstallationArgs;

            // Shortcircuit check: if the installation arguments do not match, then we should return false here to avoid other checks which are now unnecessary.
            if (!string.Equals(localPackageInstallationArgs, installationArgs))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(localPackageVersion))
            {
                tracer.Trace(string.Format("Site Extension {0} local version is {1}", id, localPackageVersion));
                SemanticVersion.TryParse(localPackageVersion, out localPkgVer);
            }

            if (!string.IsNullOrEmpty(requestInfo.PackageUri))
            {
                SemanticVersion.TryParse(requestInfo.Version, out lastFoundVer);
            }
            else
            {
                latestRemotePackage = await GetSiteExtensionInfoFromRemote(id);
                if (latestRemotePackage != null)
                {
                    tracer.Trace(string.Format("Site Extension {0} remote version is {1}", id, latestRemotePackage.Version));
                    SemanticVersion.TryParse(latestRemotePackage.Version, out lastFoundVer);
                }
            }

            if (lastFoundVer != null && localPkgVer != null && lastFoundVer <= localPkgVer)
            {
                tracer.Trace(string.Format("Site Extension with id {0} is already installed", id));
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
                throw new DirectoryNotFoundException(String.Format(CultureInfo.CurrentCulture, Resources.Error_SiteExtensionNotFound, id, installationDirectory));
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

        private string GetInstallationDirectory(string id)
        {
            return Path.Combine(_rootPath, id);
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

        private async Task SetLocalInfo(SiteExtensionInfo info)
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
                info.ExtensionUrl = string.IsNullOrEmpty(info.LocalPath) ? null : GetFullUrl(info.ExtensionUrl);
            }

            foreach (var setting in GetSettingManager(info.Id).GetValues())
            {
                if (string.Equals(setting.Key, _feedUrlSetting, StringComparison.OrdinalIgnoreCase))
                {
                    info.FeedUrl = setting.Value.Value<string>();
                }
                else if (string.Equals(setting.Key, _packageUriSetting, StringComparison.OrdinalIgnoreCase))
                {
                    info.PackageUri = setting.Value.Value<string>();
                }
                else if (string.Equals(setting.Key, _installUtcTimestampSetting, StringComparison.OrdinalIgnoreCase))
                {
                    DateTime installedDateTime;
                    if (DateTime.TryParse(setting.Value.Value<string>(), out installedDateTime))
                    {
                        info.InstalledDateTime = installedDateTime.ToUniversalTime();
                    }
                }
                else if (string.Equals(setting.Key, _installationArgs, StringComparison.OrdinalIgnoreCase))
                {
                    info.InstallationArgs = setting.Value.Value<string>();
                }
            }

            if (IsInstalledToWebRoot(info.Id))
            {
                // override WebRoot type from setting
                // WebRoot is a special type that install package to wwwroot, when perform update we need to update new content to wwwroot even if type is not specified
                info.Type = SiteExtensionInfo.SiteExtensionType.WebRoot;
            }

            if (FileSystemHelpers.DirectoryExists(localPath) && string.IsNullOrEmpty(info.PackageUri))
            {
                await TryCheckLocalPackageLatestVersionFromRemote(info);
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

        private static async Task TryCheckLocalPackageLatestVersionFromRemote(SiteExtensionInfo info, bool checkLatest = true, ITracer tracer = null)
        {
            if (checkLatest)
            {
                try
                {
                    // FindPackage gets back the latest version.
                    SiteExtensionInfo latestPackage = await GetSiteExtensionInfoFromRemote(info.Id);
                    if (latestPackage != null)
                    {
                        if (SemanticVersion.TryParse(info.Version, out SemanticVersion currentVersion)
                            && SemanticVersion.TryParse(latestPackage.Version, out SemanticVersion latestVersion))
                        {
                            info.LocalIsLatestVersion = currentVersion.Equals(latestVersion);
                        }

                        info.DownloadCount = latestPackage.DownloadCount;
                        info.PublishedDateTime = latestPackage.PublishedDateTime;
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

        public static async Task<SiteExtensionInfo> GetSiteExtensionInfoFromRemote(string id, string version = null, ITracer tracer = null)
        {
            tracer = tracer ?? NullTracer.Instance;

            SiteExtensionInfo info = null;
            using (tracer.Step("FeedExtensionsV2 search package by id: {0} and version: {1}.", id, version))
            {
                info = await FeedExtensionsV2.GetPackageByIdentity(id, version);
            }

            if (info == null)
            {
                using (tracer.Step("Fallback to FeedExtensionsV1 search package by id: {0} and version: {1}.", id, version))
                {
                    UIPackageMetadata data;
                    var remoteRepo = SiteExtensionManager.GetSourceRepository(DeploymentSettingsExtension.NuGetSiteExtensionFeedUrl);
                    if (string.IsNullOrEmpty(version))
                    {
                        data = await remoteRepo.GetLatestPackageByIdFromSrcRepo(id);
                    }
                    else
                    {
                        data = await remoteRepo.GetPackageByIdentity(id, version);
                    }

                    if (data != null)
                    {
                        info = new SiteExtensionInfo(data);
                    }
                }
            }

            if (info == null)
            {
                tracer.TraceWarning("Cannot find search package by id: {0} and version: {1}.", id, version);
            }

            return info;
        }
    }
}
