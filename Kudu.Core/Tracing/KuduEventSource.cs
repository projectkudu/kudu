using System.Diagnostics.CodeAnalysis;
using Microsoft.Diagnostics.Tracing;

namespace Kudu.Core.Tracing
{
    [EventSource(Name = "Microsoft-Windows-WebSites-Kudu")]
    public sealed class KuduEventSource : EventSource
    {
        public static readonly KuduEventSource Log = new KuduEventSource();

        [Event(65501, Level = EventLevel.Informational, Message = "Project was deployed for site {0} with result {2}", Channel = EventChannel.Operational)]
        public void ProjectDeployed(string siteName, string projectType, string result, string error, long deploymentDurationInMilliseconds, string siteMode, string scmType, string vsProjectId)
        {
            if (IsEnabled())
            {
                WriteEvent(65501, siteName, projectType, result, error, deploymentDurationInMilliseconds, siteMode, scmType, vsProjectId);
            }
        }

        [Event(65508, Level = EventLevel.Informational, Message = "WebJob {1} started for site {0}", Channel = EventChannel.Operational)]
        public void WebJobStarted(string siteName, string jobName, string scriptExtension, string jobType, string siteMode, string error, string trigger)
        {
            if (IsEnabled())
            {
                WriteEvent(65508, siteName, jobName, scriptExtension, jobType, siteMode, error, trigger);
            }
        }

        [Event(65512, Level = EventLevel.Warning, Message = "Unexpected exception for site {0}", Channel = EventChannel.Operational)]
        public void KuduException(string siteName, string method, string path, string result, string Message, string exception)
        {
            if (IsEnabled())
            {
                WriteEvent(65512, siteName, method, path, result, Message, exception);
            }
        }

        [Event(65510, Level = EventLevel.Warning, Message = "Deprecated API used for site {0}", Channel = EventChannel.Operational)]
        public void DeprecatedApiUsed(string siteName, string route, string userAgent, string method, string path)
        {
            if (IsEnabled())
            {
                WriteEvent(65510, siteName, route, userAgent, method, path);
            }
        }

        [Event(65511, Level = EventLevel.Informational, Message = "SiteExtension action for site {0}", Channel = EventChannel.Operational)]
        public void KuduSiteExtensionEvent(string siteName, string method, string path, string result, string deploymentDurationInMilliseconds, string Message)
        {
            if (IsEnabled())
            {
                WriteEvent(65511, siteName, method, path, result, deploymentDurationInMilliseconds, Message);
            }
        }

        [Event(65513, Level = EventLevel.Informational, Message = "WebJob {1} event for site {0}", Channel = EventChannel.Operational)]
        public void WebJobEvent(string siteName, string jobName, string Message, string jobType, string error)
        {
            if (IsEnabled())
            {
                WriteEvent(65513, siteName, jobName, Message, jobType, error);
            }
        }

        [SuppressMessage("Microsoft.Performance", "CA1801:ReviewUnusedParameters")]
        [Event(65514, Level = EventLevel.Informational, Message = "Generic event for site {0}", Channel = EventChannel.Operational)]
        public void GenericEvent(string siteName, string Message, string requestId, string scmType, string siteMode, string buildVersion)
        {
            if (IsEnabled())
            {
                WriteEvent(65514, siteName, Message, requestId, scmType, siteMode, buildVersion);
            }
        }

        [SuppressMessage("Microsoft.Performance", "CA1801:ReviewUnusedParameters")]
        [Event(65515, Level = EventLevel.Informational, Message = "Api event for site {0}", Channel = EventChannel.Operational)]
        public void ApiEvent(string siteName, string Message, string address, string verb, string requestId, int statusCode, long latencyInMilliseconds, string userAgent)
        {
            if (IsEnabled())
            {
                WriteEvent(65515, siteName, Message, address, verb, requestId, statusCode, latencyInMilliseconds, userAgent);
            }
        }

        /// <summary>
        /// DeploymentCompleted event
        /// </summary>
        /// <param name="siteName">WEBSITE_SITE_NAME</param>
        /// <param name="kind">MSDeploy, ZipDeploy, Git, ...</param>
        /// <param name="requestId">requestId</param>
        /// <param name="status">Success, Failed</param>
        /// <param name="details">deployment-specific json</param>
        [SuppressMessage("Microsoft.Performance", "CA1801:ReviewUnusedParameters")]
        [Event(65516, Level = EventLevel.Informational, Message = "Deployment completed for site {0}", Channel = EventChannel.Operational)]
        public void DeploymentCompleted(string siteName, string kind, string requestId, string status, string details)
        {
            if (IsEnabled())
            {
                WriteEvent(65516, siteName, kind, requestId, status, details);
            }
        }
    }
}
