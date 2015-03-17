using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        public SiteExtensionController(ISiteExtensionManager manager, IEnvironment environment, ITraceFactory traceFactory, IAnalytics analytics)
        {
            _manager = manager;
            _environment = environment;
            _traceFactory = traceFactory;
            _analytics = analytics;
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
                    var installationLock = SiteExtensionInstallationLock.CreateLock(_environment.SiteExtensionSettingsPath, id);
                    if (!installationLock.IsHeld
                        && string.Equals(Constants.SiteExtensionProvisioningStateSucceeded, armSettings.ProvisioningState, StringComparison.OrdinalIgnoreCase))
                    {
                        extension = await _manager.GetLocalExtension(id, checkLatest);
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
                            if (SiteExtensionInstallationLock.IsAnyPendingLock(_environment.SiteExtensionSettingsPath))
                            {
                                using (tracer.Step("{0} finsihed installation. But there is other installation on-going, fake the status to be Created, so that we can restart once for all.", id))
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
                                bool requireRestart = SiteExtensionStatus.IsAnyInstallationRequireRestart(_environment.SiteExtensionSettingsPath, Path.Combine(_environment.RootPath, "SiteExtensions"), tracer, _analytics);
                                // clear operation, since opeation is done
                                if (UpdateArmSettingsForSuccessInstallation())
                                {
                                    using (tracer.Step("{0} finsihed installation and batch update lock aquired. Will notify Antares GEO to restart website.", id))
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
                    else if (!installationLock.IsHeld && !armSettings.IsTerminalStatus())
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
                    tracer.Trace("ARM get : {0}", id);
                    extension = await _manager.GetLocalExtension(id, checkLatest);
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
                tracer.Trace("Get : {0}", id);
                extension = await _manager.GetLocalExtension(id, checkLatest);

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
            return await InstallExtension(id, requestInfo.Properties);
        }

        [HttpPut]
        public async Task<HttpResponseMessage> InstallExtension(string id, SiteExtensionInfo requestInfo)
        {
            var tracer = _traceFactory.GetTracer();
            var installationLock = SiteExtensionInstallationLock.CreateLock(_environment.SiteExtensionSettingsPath, id);
            if (installationLock.IsHeld)
            {
                tracer.Trace("{0} is installing with another request, reject current request with Conflict status.", id);
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.Conflict, id));
            }

            if (requestInfo == null)
            {
                requestInfo = new SiteExtensionInfo();
            }

            SiteExtensionInfo result = await InitInstallSiteExtension(id, requestInfo.Type);

            if (ArmUtils.IsArmRequest(Request))
            {
                // create a context free tracer
                ITracer backgroundTracer = NullTracer.Instance;
                IDictionary<string, string> traceAttributes = new Dictionary<string, string>();

                if (tracer.TraceLevel == TraceLevel.Off)
                {
                    backgroundTracer = NullTracer.Instance;
                }

                if (tracer.TraceLevel > TraceLevel.Off)
                {
                    backgroundTracer = new XmlTracer(_environment.TracePath, tracer.TraceLevel);
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

                // trigger installation, but do not wait. Expecting poll for status
                ThreadPool.QueueUserWorkItem((object stateInfo) =>
                {
                    using (backgroundTracer.Step(XmlTracer.BackgroundTrace, attributes: traceAttributes))
                    {
                        _manager.InstallExtension(id, requestInfo.Version, requestInfo.FeedUrl, requestInfo.Type, backgroundTracer).Wait();
                    }
                });

                return Request.CreateResponse(HttpStatusCode.Created, ArmUtils.AddEnvelopeOnArmRequest<SiteExtensionInfo>(result, Request));
            }
            else
            {
                result = await _manager.InstallExtension(id, requestInfo.Version, requestInfo.FeedUrl, requestInfo.Type, tracer);

                if (string.Equals(Constants.SiteExtensionProvisioningStateFailed, result.ProvisioningState, StringComparison.OrdinalIgnoreCase))
                {
                    SiteExtensionStatus armSettings = new SiteExtensionStatus(_environment.SiteExtensionSettingsPath, id, tracer);
                    throw new HttpResponseException(Request.CreateErrorResponse(armSettings.Status, result.Comment));
                }

                return Request.CreateResponse(HttpStatusCode.OK, result);
            }
        }

        [HttpDelete]
        public async Task<HttpResponseMessage> UninstallExtension(string id)
        {
            try
            {
                bool isUninstalled = await _manager.UninstallExtension(id);
                if (ArmUtils.IsArmRequest(Request))
                {
                    if (isUninstalled)
                    {
                        return Request.CreateResponse(HttpStatusCode.OK);
                    }
                    else
                    {
                        var extension = new SiteExtensionInfo { Id = id };
                        return Request.CreateResponse(HttpStatusCode.BadRequest, ArmUtils.AddEnvelopeOnArmRequest<SiteExtensionInfo>(extension, Request));
                    }
                }
                else
                {
                    return Request.CreateResponse(HttpStatusCode.OK, isUninstalled);
                }
            }
            catch (DirectoryNotFoundException ex)
            {
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.NotFound, ex));
            }
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
            var batchUpdateLock = SiteExtensionBatchUpdateStatusLock.CreateLock(_environment.SiteExtensionSettingsPath);

            bool isAnyUpdate = false;

            bool islocked = batchUpdateLock.TryLockOperation(() =>
            {
                var tracer = _traceFactory.GetTracer();
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
                        }
                        catch (Exception ex)
                        {
                            tracer.TraceError(ex);
                            // no-op
                        }
                    }
                }

            }, TimeSpan.FromSeconds(5));

            return islocked && isAnyUpdate;
        }
    }
}
