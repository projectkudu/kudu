using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.SiteExtensions;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Core.Infrastructure;
using Kudu.Core.Settings;
using Kudu.Core.SiteExtensions;
using Kudu.Core.Tracing;
using Kudu.Services.Arm;

namespace Kudu.Services.SiteExtensions
{
    [ArmControllerConfiguration]
    [EnsureRequestIdHandler]
    public class SiteExtensionController : ApiController
    {
        private readonly ISiteExtensionManager _manager;
        private readonly IEnvironment _environment;
        private readonly ITraceFactory _traceFactory;
        private readonly IAnalytics _analytics;
        private readonly string _siteExtensionRoot;

        // List of packages that had to be renamed when moving to nuget.org because the siteextension.org id conflicted
        // with an existing nuget.org id
        static Dictionary<string, string> _packageIdRedirects = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "python2714x64", "azureappservice-python2714x64" },
            { "python2714x86", "azureappservice-python2714x86" },
            { "python353x64", "azureappservice-python353x64" },
            { "python353x86", "azureappservice-python353x86" },
            { "python354x64", "azureappservice-python354x64" },
            { "python354x86", "azureappservice-python354x86" },
            { "python362x64", "azureappservice-python362x64" },
            { "python362x86", "azureappservice-python362x86" },
            { "python364x64", "azureappservice-python364x64" },
            { "python364x86", "azureappservice-python364x86" },
            { "NewRelic.Azure.WebSites", "NewRelic.Azure.WebSites.Extension"}
        };

        public SiteExtensionController(ISiteExtensionManager manager, IEnvironment environment, ITraceFactory traceFactory, IAnalytics analytics)
        {
            _manager = manager;
            _environment = environment;
            _traceFactory = traceFactory;
            _analytics = analytics;
            _siteExtensionRoot = Path.Combine(_environment.RootPath, "SiteExtensions");
        }

        [HttpGet]
        public async Task<HttpResponseMessage> GetRemoteExtensions(string filter = null, bool allowPrereleaseVersions = false, string feedUrl = null)
        {
            return Request.CreateResponse(
                HttpStatusCode.OK,
                ArmUtils.AddEnvelopeOnArmRequest<SiteExtensionInfo>(await _manager.GetRemoteExtensions(filter, allowPrereleaseVersions, feedUrl), Request));
        }

        [HttpGet]
        public async Task<HttpResponseMessage> GetRemoteExtension(string id, string version = null, string feedUrl = null)
        {
            SiteExtensionInfo extension = await _manager.GetRemoteExtension(id, version, feedUrl);

            if (extension == null)
            {
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.NotFound, id));
            }

            return Request.CreateResponse(
                HttpStatusCode.OK,
                ArmUtils.AddEnvelopeOnArmRequest<SiteExtensionInfo>(extension, Request));
        }

        [HttpGet]
        public async Task<HttpResponseMessage> GetLocalExtensions(string filter = null, bool checkLatest = true)
        {
            return Request.CreateResponse(
                HttpStatusCode.OK,
                ArmUtils.AddEnvelopeOnArmRequest<SiteExtensionInfo>(await _manager.GetLocalExtensions(filter, checkLatest), Request));
        }

        [HttpGet]
        public async Task<HttpResponseMessage> GetLocalExtension(string id, bool checkLatest = true)
        {
            var tracer = _traceFactory.GetTracer();

            SiteExtensionInfo extension = null;
            HttpResponseMessage responseMessage = null;
            if (ArmUtils.IsArmRequest(Request))
            {
                tracer.Trace("Incoming GetLocalExtension is arm request.");
                SiteExtensionStatus armSettings = new SiteExtensionStatus(_environment.SiteExtensionSettingsPath, id, tracer);

                if (string.Equals(Constants.SiteExtensionOperationInstall, armSettings.Operation, StringComparison.OrdinalIgnoreCase))
                {
                    bool isInstallationLockHeld = IsInstallationLockHeldSafeCheck(id);
                    if (!isInstallationLockHeld
                        && string.Equals(Constants.SiteExtensionProvisioningStateSucceeded, armSettings.ProvisioningState, StringComparison.OrdinalIgnoreCase))
                    {
                        tracer.Trace("Package {0} was just installed.", id);
                        extension =  await ThrowsConflictIfIOException(_manager.GetLocalExtension(id, checkLatest));
                        if (extension == null)
                        {
                            using (tracer.Step("Status indicate {0} installed, but not able to find it from local repo.", id))
                            {
                                // should NOT happen
                                extension = new SiteExtensionInfo { Id = id };
                                responseMessage = Request.CreateResponse(HttpStatusCode.NotFound);
                                // package is gone, remove setting file
                                await armSettings.RemoveStatus();
                            }
                        }
                        else
                        {
                            if (SiteExtensionInstallationLock.IsAnyPendingLock(_environment.SiteExtensionSettingsPath, tracer))
                            {
                                using (tracer.Step("{0} finished installation. But there is other installation on-going, fake the status to be Created, so that we can restart once for all.", id))
                                {
                                    // if there is other pending installation, fake the status
                                    extension.ProvisioningState = Constants.SiteExtensionProvisioningStateCreated;
                                    responseMessage = Request.CreateResponse(HttpStatusCode.Created, ArmUtils.AddEnvelopeOnArmRequest<SiteExtensionInfo>(extension, Request));
                                }
                            }
                            else
                            {
                                // it is important to call "SiteExtensionStatus.IsAnyInstallationRequireRestart" before "UpdateArmSettingsForSuccessInstallation"
                                // since "IsAnyInstallationRequireRestart" is depending on properties inside site extension status files 
                                // while "UpdateArmSettingsForSuccessInstallation" will override some of the values
                                bool requireRestart = SiteExtensionStatus.IsAnyInstallationRequireRestart(_environment.SiteExtensionSettingsPath, _siteExtensionRoot, tracer, _analytics);
                                // clear operation, since opeation is done
                                if (UpdateArmSettingsForSuccessInstallation())
                                {
                                    using (tracer.Step("{0} finished installation and batch update lock aquired. Will notify Antares GEO to restart website.", id))
                                    {
                                        responseMessage = Request.CreateResponse(armSettings.Status, ArmUtils.AddEnvelopeOnArmRequest<SiteExtensionInfo>(extension, Request));

                                        // Notify GEO to restart website if necessary
                                        if (requireRestart)
                                        {
                                            responseMessage.Headers.Add(Constants.SiteOperationHeaderKey, Constants.SiteOperationRestart);
                                        }
                                    }
                                }
                                else
                                {
                                    tracer.Trace("Not able to aquire batch update lock, there must be another batch update on-going. return Created status to user to let them poll again.");
                                    responseMessage = Request.CreateResponse(HttpStatusCode.Created, ArmUtils.AddEnvelopeOnArmRequest<SiteExtensionInfo>(extension, Request));
                                }
                            }
                        }
                    }
                    else if (!isInstallationLockHeld && !armSettings.IsTerminalStatus())
                    {
                        // no background thread is working on instalation
                        // app-pool must be recycled
                        using (tracer.Step("{0} installation cancelled, background thread must be dead.", id))
                        {
                            extension = new SiteExtensionInfo { Id = id };
                            extension.ProvisioningState = Constants.SiteExtensionProvisioningStateCanceled;
                            responseMessage = Request.CreateResponse(HttpStatusCode.OK, ArmUtils.AddEnvelopeOnArmRequest<SiteExtensionInfo>(extension, Request));
                        }
                    }
                    else
                    {
                        // on-going or failed, return status from setting
                        using (tracer.Step("Installation {0}", armSettings.Status))
                        {
                            extension = new SiteExtensionInfo { Id = id };
                            armSettings.FillSiteExtensionInfo(extension);
                            responseMessage = Request.CreateResponse(armSettings.Status, ArmUtils.AddEnvelopeOnArmRequest<SiteExtensionInfo>(extension, Request));
                        }
                    }
                }

                // normal GET request
                if (responseMessage == null)
                {
                    using (tracer.Step("ARM get : {0}", id))
                    {
                        extension = await ThrowsConflictIfIOException(_manager.GetLocalExtension(id, checkLatest));
                    }

                    if (extension == null)
                    {
                        extension = new SiteExtensionInfo { Id = id };
                        responseMessage = Request.CreateResponse(HttpStatusCode.NotFound);
                    }
                    else
                    {
                        armSettings.FillSiteExtensionInfo(extension);
                        responseMessage = Request.CreateResponse(HttpStatusCode.OK, ArmUtils.AddEnvelopeOnArmRequest<SiteExtensionInfo>(extension, Request));
                    }
                }
            }
            else
            {
                using (tracer.Step("Get: {0}, is not a ARM request.", id))
                {
                    extension = await ThrowsConflictIfIOException(_manager.GetLocalExtension(id, checkLatest));
                }

                if (extension == null)
                {
                    throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.NotFound, id));
                }

                responseMessage = Request.CreateResponse(HttpStatusCode.OK, extension);
            }

            return responseMessage;
        }

        [HttpPut]
        public async Task<HttpResponseMessage> InstallExtensionArm(string id, ArmEntry<SiteExtensionInfo> requestInfo)
        {
            if (requestInfo == null)
            {
                // Body should not be empty
                return Request.CreateResponse(HttpStatusCode.BadRequest);
            }

            return await InstallExtension(id, requestInfo.Properties);
        }

        [HttpPut]
        public async Task<HttpResponseMessage> InstallExtension(string id, SiteExtensionInfo requestInfo)
        {
            var startTime = DateTime.UtcNow;
            var tracer = _traceFactory.GetTracer();

            // If there is an id redirect for it, switch to the new id
            if (_packageIdRedirects.TryGetValue(id, out string newId))
            {
                tracer.Trace($"Package id '{id}' was redirected to id '{newId}.");
                id = newId;
            }

            if (IsInstallationLockHeldSafeCheck(id))
            {
                tracer.Trace("{0} is installing with another request, reject current request with Conflict status.", id);
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.Conflict, id));
            }

            if (requestInfo == null)
            {
                requestInfo = new SiteExtensionInfo();
            }

            tracer.Trace("Installing {0}, version: {1} from feed: {2}", id, requestInfo.Version, requestInfo.FeedUrl);
            SiteExtensionInfo result = await InitInstallSiteExtension(id, requestInfo.Type);

            if (ArmUtils.IsArmRequest(Request))
            {
                // create a context free tracer
                ITracer backgroundTracer = NullTracer.Instance;
                IDictionary<string, string> traceAttributes = new Dictionary<string, string>();

                if (tracer.TraceLevel > TraceLevel.Off)
                {
                    backgroundTracer = new CascadeTracer(new XmlTracer(_environment.TracePath, tracer.TraceLevel), new ETWTracer(_environment.RequestId, "PUT"));
                    traceAttributes = new Dictionary<string, string>()
                    {
                        {"url", Request.RequestUri.AbsolutePath},
                        {"method", Request.Method.Method}
                    };

                    foreach (var item in Request.Headers)
                    {
                        if (!traceAttributes.ContainsKey(item.Key))
                        {
                            traceAttributes.Add(item.Key, string.Join(",", item.Value));
                        }
                    }
                }

                AutoResetEvent installationSignal = new AutoResetEvent(false);

                // trigger installation, but do not wait. Expecting poll for status
                ThreadPool.QueueUserWorkItem((object stateInfo) =>
                {
                    using (backgroundTracer.Step(XmlTracer.BackgroundTrace, attributes: traceAttributes))
                    {
                        try
                        {
                            using (backgroundTracer.Step("Background thread started for {0} installation", id))
                            {
                                _manager.InstallExtension(id, requestInfo.Version, requestInfo.FeedUrl, requestInfo.Type, backgroundTracer, requestInfo.InstallationArgs).Wait();
                            }
                        }
                        finally
                        {
                            installationSignal.Set();

                            // will be a few millionseconds off if task finshed within 15 seconds.
                            LogEndEvent(id, (DateTime.UtcNow - startTime), backgroundTracer);
                        }
                    }
                });

                SiteExtensionStatus armSettings = new SiteExtensionStatus(_environment.SiteExtensionSettingsPath, id, tracer);
                if (installationSignal.WaitOne(TimeSpan.FromSeconds(15)))
                {
                    if (!armSettings.IsRestartRequired(_siteExtensionRoot))
                    {
                        // only skip polling if current installation doesn`t require restart, to avoid making race condition common
                        // TODO: re-visit if we want to skip polling for case that need to restart
                        tracer.Trace("Installation finish quick and not require restart, skip async polling, invoking GET to return actual status to caller.");
                        return await GetLocalExtension(id);
                    }
                }

                // do not log end event here, since it is not done yet
                return Request.CreateResponse(HttpStatusCode.Created, ArmUtils.AddEnvelopeOnArmRequest<SiteExtensionInfo>(result, Request));
            }
            else
            {
                result = await _manager.InstallExtension(id, requestInfo.Version, requestInfo.FeedUrl, requestInfo.Type, tracer, requestInfo.InstallationArgs);

                if (string.Equals(Constants.SiteExtensionProvisioningStateFailed, result.ProvisioningState, StringComparison.OrdinalIgnoreCase))
                {
                    SiteExtensionStatus armSettings = new SiteExtensionStatus(_environment.SiteExtensionSettingsPath, id, tracer);
                    throw new HttpResponseException(Request.CreateErrorResponse(armSettings.Status, result.Comment));
                }

                var response = Request.CreateResponse(HttpStatusCode.OK, result);
                LogEndEvent(id, (DateTime.UtcNow - startTime), tracer);
                return response;
            }
        }

        [HttpDelete]
        public async Task<HttpResponseMessage> UninstallExtension(string id)
        {
            var startTime = DateTime.UtcNow;
            var tracer = _traceFactory.GetTracer();
            try
            {
                HttpResponseMessage response = null;
                bool isUninstalled = await _manager.UninstallExtension(id);
                if (ArmUtils.IsArmRequest(Request))
                {
                    if (isUninstalled)
                    {
                        response = Request.CreateResponse(HttpStatusCode.OK);
                    }
                    else
                    {
                        var extension = new SiteExtensionInfo { Id = id };
                        response = Request.CreateResponse(HttpStatusCode.BadRequest, ArmUtils.AddEnvelopeOnArmRequest<SiteExtensionInfo>(extension, Request));
                    }
                }
                else
                {
                    response = Request.CreateResponse(HttpStatusCode.OK, isUninstalled);
                }

                LogEndEvent(id, (DateTime.UtcNow - startTime), tracer, defaultResult: Constants.SiteExtensionProvisioningStateSucceeded);
                return response;
            }
            catch (DirectoryNotFoundException ex)
            {
                _analytics.UnexpectedException(
                        ex,
                        method: "DELETE",
                        path: string.Format(CultureInfo.InvariantCulture, "/api/siteextensions/{0}", id),
                        result: Constants.SiteExtensionProvisioningStateFailed,
                        message: null,
                        trace: false);

                tracer.TraceError(ex, "Failed to uninstall {0}", id);
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.NotFound, ex));
            }
            catch (Exception ex)
            {
                _analytics.UnexpectedException(
                        ex,
                        method: "DELETE",
                        path: string.Format(CultureInfo.InvariantCulture, "/api/siteextensions/{0}", id),
                        result: Constants.SiteExtensionProvisioningStateFailed,
                        message: null,
                        trace: false);

                tracer.TraceError(ex, "Failed to uninstall {0}", id);
                throw ex;
            }
        }

        /// <summary>
        /// Log to MDS when installation/uninstallation finishes
        /// </summary>
        private void LogEndEvent(string id, TimeSpan duration, ITracer tracer, string defaultResult = null)
        {
            SiteExtensionStatus armStatus = new SiteExtensionStatus(_environment.SiteExtensionSettingsPath, id, tracer);
            string filePath = Path.Combine(_environment.RootPath, "SiteExtensions", id, "SiteExtensionSettings.json");
            var jsonSetting = new JsonSettings(filePath);
            _analytics.SiteExtensionEvent(
                Request.Method.Method,
                Request.RequestUri.AbsolutePath,
                armStatus.ProvisioningState ?? defaultResult,
                duration.TotalMilliseconds.ToString(),
                jsonSetting.ToString());
        }

        private async Task<SiteExtensionInfo> InitInstallSiteExtension(string id, SiteExtensionInfo.SiteExtensionType type)
        {
            SiteExtensionStatus settings = new SiteExtensionStatus(_environment.SiteExtensionSettingsPath, id, _traceFactory.GetTracer());
            settings.ProvisioningState = Constants.SiteExtensionProvisioningStateCreated;
            settings.Operation = Constants.SiteExtensionOperationInstall;
            settings.Status = HttpStatusCode.Created;
            settings.Type = type;
            settings.Comment = null;

            SiteExtensionInfo info = new SiteExtensionInfo();
            info.Id = id;
            settings.FillSiteExtensionInfo(info);
            return await Task.FromResult(info);
        }

        /// <summary>
        /// <para>1. list all package</para>
        /// <para>2. for each package: if operation is 'install' and provisionState is 'success' and no installation lock</para>
        /// <para>      Update operation to null</para>
        /// </summary>
        private bool UpdateArmSettingsForSuccessInstallation()
        {
            var tracer = _traceFactory.GetTracer();
            using (tracer.Step("Checking if there is any installation finished recently, if there is one, update its status."))
            {
                var batchUpdateLock = SiteExtensionBatchUpdateStatusLock.CreateLock(_environment.SiteExtensionSettingsPath);

                bool isAnyUpdate = false;

                try
                {
                    batchUpdateLock.LockOperation(() =>
                    {
                        string[] packageDirs = FileSystemHelpers.GetDirectories(_environment.SiteExtensionSettingsPath);
                        foreach (var dir in packageDirs)
                        {
                            var dirInfo = new DirectoryInfo(dir);   // arm setting folder name is same as package id
                            SiteExtensionStatus armSettings = new SiteExtensionStatus(_environment.SiteExtensionSettingsPath, dirInfo.Name, tracer);
                            if (string.Equals(armSettings.Operation, Constants.SiteExtensionOperationInstall, StringComparison.OrdinalIgnoreCase)
                                && string.Equals(armSettings.ProvisioningState, Constants.SiteExtensionProvisioningStateSucceeded, StringComparison.OrdinalIgnoreCase))
                            {
                                try
                                {
                                    armSettings.Operation = null;
                                    isAnyUpdate = true;
                                    tracer.Trace("Updated {0}", dir);
                                }
                                catch (Exception ex)
                                {
                                    tracer.TraceError(ex);
                                    // no-op
                                }
                            }
                        }

                    }, "Updating SiteExtension success status", TimeSpan.FromSeconds(5));

                    return isAnyUpdate;
                }
                catch (LockOperationException)
                {
                    return false;
                }
            }
        }

        private bool IsInstallationLockHeldSafeCheck(string id)
        {
            SiteExtensionInstallationLock installationLock = null;
            try
            {
                installationLock = SiteExtensionInstallationLock.CreateLock(_environment.SiteExtensionSettingsPath, id);
                return installationLock.IsHeld;
            }
            finally
            {
                if (installationLock != null)
                {
                    installationLock.Release();
                }
            }
        }

        private async Task<T> ThrowsConflictIfIOException<T>(Task<T> task)
        {
            try
            {
                return await task;
            }
            catch (IOException ex)
            {
                // Simplify the exception handler by converting any IOException 
                // to 409 Conflict instead of 500 InternalServerError (implying server issue).
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.Conflict, ex));
            }
        }
    }
}
