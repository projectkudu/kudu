using Kudu.Core.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Kudu.Contracts.Tracing;
using Newtonsoft.Json;
using Kudu.Core.Tracing;
using System.Linq;

namespace Kudu.Core.Deployment
{
    public class DeploymentLogger : ILogger
    {
        private readonly ILogger _innerLogger;
        private readonly ITracer _tracer;
        private DeploymentInfoBase _deploymentInfo;
        private Dictionary<string, string> siteInfo;
        private string siteInfoMsg = string.Empty;

        public DeploymentLogger(ILogger innerLogger, ITracer tracer, DeploymentInfoBase deploymentInfo)
        {
            _innerLogger = innerLogger;
            _tracer = tracer;
            _deploymentInfo = deploymentInfo;
            SetupFieldsFromEnv();
        }

        public ILogger Log(string value, LogEntryType type)
        {
            if (!string.IsNullOrEmpty(value))
            {
                string deploymentStatus = string.Empty;
                deploymentStatus = type == LogEntryType.Error ? "Error" :
                                        value == Resources.Log_DeploymentSuccessful ? "Success" :
                                        value == Resources.Log_DeploymentFailed ? "Failed" :
                                        String.Empty;

                if (!string.IsNullOrEmpty(deploymentStatus))
                {
                    _tracer.Trace("{0} DEPLOYMENTINFO: {1}, deploymentStatus = {2}", value, siteInfoMsg, deploymentStatus);
                }
                else _tracer.Trace(value);
            }

            //return NullLogger.Instance;
            return new CascadeLogger(new DeploymentLogger(NullLogger.Instance, _tracer, _deploymentInfo), _innerLogger.Log(value, type));
        }

        private void SetupFieldsFromEnv()
        {
            siteInfo = new Dictionary<string, string>();

            siteInfo["deploymentId"] = _deploymentInfo?.DeploymentTrackingId;
            siteInfo["correlationId"] = _deploymentInfo?.CorrelationId;
            siteInfo["deploymentPath"] = _deploymentInfo?.DeploymentPath;
            siteInfo["os"] = System.Environment.GetEnvironmentVariable("WEBSITE_OS");
            siteInfo["sku"] = System.Environment.GetEnvironmentVariable("WEBSITE_SKU");
            siteInfo["language"] = System.Environment.GetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME");
            //siteInfo["frameworkVersion"] = System.Environment.GetEnvironmentVariable("FRAMEWORK_VERSION"); //linux only
            siteInfo["siteName"] = System.Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME");
            siteInfo["slotName"] = System.Environment.GetEnvironmentVariable("WEBSITE_SLOT_NAME");
            siteInfo["siteHostName"] = System.Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME");

            siteInfoMsg = string.Join(", ", siteInfo.Where(x => !string.IsNullOrEmpty(x.Value)).Select(x => x.Key + " = " + x.Value).ToArray());
        }
    }
}
