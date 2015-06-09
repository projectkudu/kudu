using Microsoft.Diagnostics.Tracing;

namespace Kudu.Core.Tracing
{
    [EventSource(Name = "Microsoft-Windows-WebSites-Kudu")]
    public sealed class KuduEventSource : EventSource
    {
        public static readonly KuduEventSource Log = new KuduEventSource();

        [Event(65501, Level = EventLevel.Informational, Message = "Project was deployed for site {0} with result {2}", Channel = EventChannel.Operational)]
        public void ProjectDeployed(string siteName, string projectType, string result, string error, long deploymentDurationInMilliseconds, string siteMode, string scmType)
        {
            if (IsEnabled())
            {
                WriteEvent(65501, siteName, projectType, result, error, deploymentDurationInMilliseconds, siteMode, scmType);
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

        // TODO: once next Antares is out, removed event 65509, and only keep 65512
        [Event(65509, Level = EventLevel.Warning, Message = "Unexpected exception for site {0}", Channel = EventChannel.Operational)]
        public void KuduUnexpectedException(string siteName, string exception)
        {
            if (IsEnabled())
            {
                WriteEvent(65509, siteName, exception);
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
    }
}
