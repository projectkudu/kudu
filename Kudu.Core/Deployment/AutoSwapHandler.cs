using System;
using System.IO;
using System.IO.Abstractions;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
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

            FileInfoBase autoSwapLockFile = FileSystemHelpers.FileInfoFromFileName(_autoSwapLockFilePath);

            // Auto swap is ongoing if the auto swap lock file exists and is written to less than 2 minutes ago
            bool isAutoSwapOngoing = autoSwapLockFile.Exists && autoSwapLockFile.LastWriteTimeUtc >= DateTime.UtcNow.AddMinutes(-2);
            if (isAutoSwapOngoing)
            {
                _traceFactory.GetTracer().Trace("There is currently an ongoing auto swap deployment");
            }

            return isAutoSwapOngoing;
        }

        public async Task HandleAutoSwap(string currentDeploymetId, DeploymentContext context)
        {
            ITracer tracer = context.Tracer;
            if (!IsAutoSwapEnabled())
            {

                tracer.Trace("AutoSwap is not enabled");
                return;
            }

            string jwtToken = System.Environment.GetEnvironmentVariable(Constants.SiteRestrictedJWT);
            if (string.IsNullOrWhiteSpace(jwtToken))
            {
                tracer.Trace("Jwt token is null");
                return;
            }

            // active deployment is always a success deployment
            string lastDeploymentId = _deploymentStatusManager.ActiveDeploymentId;
            if (string.Equals(currentDeploymetId, lastDeploymentId, StringComparison.OrdinalIgnoreCase))
            {
                tracer.Trace("Deployment haven't changed, no need for auto swap: {0}", lastDeploymentId);
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

            var queryStrings = HttpUtility.ParseQueryString(string.Empty);
            queryStrings["slot"] = _autoSwapSlotName;
            queryStrings["operationId"] = operationId;

            var client = new OperationClient(context.Tracer);
            await client.PostAsync<string>("/operations/autoswap?" + queryStrings.ToString());
            context.Logger.Log("Requesting auto swap to slot - '{0}' operation id - '{1}' deployment id - '{2}'".FormatInvariant(_autoSwapSlotName, operationId, currentDeploymetId));
        }

        public bool IsAutoSwapEnabled()
        {
            return !String.IsNullOrEmpty(_autoSwapSlotName);
        }
    }
}
