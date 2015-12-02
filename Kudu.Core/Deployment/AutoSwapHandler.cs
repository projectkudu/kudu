using System;
using System.Configuration;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Web;
using Kudu.Contracts.Settings;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;

namespace Kudu.Core.Deployment
{
    public class AutoSwapHandler : IAutoSwapHandler
    {
        public const string AutoSwapLockFile = "autoswap.lock";

        private readonly IDeploymentStatusManager _deploymentStatusManager;
        private readonly ITraceFactory _traceFactory;
        private readonly string _autoSwapSlotName;
        private readonly string _autoSwapLockFilePath;
        private string _initialActiveDeplymentId;

        public AutoSwapHandler(IDeploymentStatusManager deploymentStatusManager, IEnvironment environment, IDeploymentSettingsManager settings, ITraceFactory traceFactory)
        {
            _deploymentStatusManager = deploymentStatusManager;
            _traceFactory = traceFactory;
            _autoSwapSlotName = settings.GetValue("WEBSITE_SWAP_SLOTNAME");
            _autoSwapLockFilePath = Path.Combine(environment.LocksPath, AutoSwapLockFile);
        }

        public bool IsAutoSwapOngoing()
        {
            if (!IsAutoSwapEnabled())
            {
                return false;
            }

            _initialActiveDeplymentId = _deploymentStatusManager.ActiveDeploymentId;

            FileInfoBase autoSwapLockFile = FileSystemHelpers.FileInfoFromFileName(_autoSwapLockFilePath);

            // Auto swap is ongoing if the auto swap lock file exists and is written to less than 2 minutes ago
            bool isAutoSwapOngoing = autoSwapLockFile.Exists && autoSwapLockFile.LastWriteTimeUtc >= DateTime.UtcNow.AddMinutes(-2);
            if (isAutoSwapOngoing)
            {
                _traceFactory.GetTracer().Trace("There is currently an ongoing auto swap deployment");
            }

            return isAutoSwapOngoing;
        }

        public void HandleAutoSwap()
        {
            var tracer = _traceFactory.GetTracer();
            if (!IsAutoSwapEnabled())
            {
                return;
            }

            // active deployment is always a success deployment
            string currentActiveDeploymentId = _deploymentStatusManager.ActiveDeploymentId;
            if (currentActiveDeploymentId == _initialActiveDeplymentId)
            {
                tracer.Trace("Deployment haven't changed, no need for auto swap: {0}", currentActiveDeploymentId);
                return;
            }

            try
            {
                FileSystemHelpers.WriteAllTextToFile(_autoSwapLockFilePath, String.Empty);
            }
            catch (Exception ex)
            {
                tracer.TraceError(ex);
            }

            string operationId = "AUTOSWAP" + Guid.NewGuid();
            string deploymentId = currentActiveDeploymentId;

            HttpResponse response = HttpContext.Current.Response;

            response.Headers.Add("X-MS-SWAP-OPERATIONID", operationId);
            response.Headers.Add("X-MS-SWAP-SLOTNAME", _autoSwapSlotName);
            response.Headers.Add("X-MS-SWAP-DEPLOYMENTID", deploymentId);

            tracer.Trace("Requesting auto swap to slot name - '{0}', operation id - '{1}', deployment id - '{2}'".FormatInvariant(_autoSwapSlotName, operationId, deploymentId));
        }

        public bool IsAutoSwapEnabled()
        {
            return !String.IsNullOrEmpty(_autoSwapSlotName);
        }
    }
}
