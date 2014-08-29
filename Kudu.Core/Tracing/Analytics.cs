using System;
using System.Text;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;

namespace Kudu.Core.Tracing
{
    public class Analytics : IAnalytics
    {
        private readonly SiteExtensionLogManager _siteExtensionLogManager;
        private readonly IDeploymentSettingsManager _settings;

        public Analytics(IDeploymentSettingsManager settings, ITracer tracer, string directoryPath)
        {
            _settings = settings;
            _siteExtensionLogManager = new SiteExtensionLogManager(tracer, directoryPath);
        }

        public void ProjectDeployed(string projectType, string result, string error, long deploymentDurationInMilliseconds, string siteMode)
        {
            var o = new KuduSiteExtensionLogEvent("SiteDeployed");
            o["SiteType"] = projectType;
            o["ScmType"] = _settings.GetValue(SettingsKeys.ScmType);
            o["Result"] = result;
            o["Error"] = error;
            o["Latency"] = deploymentDurationInMilliseconds;
            o["SiteMode"] = siteMode;

            _siteExtensionLogManager.Log(o);
        }

        public void JobStarted(string jobName, string scriptExtension, string jobType, string siteMode, string error)
        {
            var o = new KuduSiteExtensionLogEvent("JobStarted");
            o["JobName"] = jobName;
            o["ScriptExtension"] = scriptExtension;
            o["JobType"] = jobType;
            o["SiteMode"] = siteMode;
            o["Error"] = error;

            _siteExtensionLogManager.Log(o);
        }

        public void UnexpectedException(Exception exception)
        {
            var strb = new StringBuilder();
            strb.AppendLine(exception.ToString());

            var aggregate = exception as AggregateException;
            if (aggregate != null)
            {
                foreach (var inner in aggregate.Flatten().InnerExceptions)
                {
                    strb.AppendLine(inner.ToString());
                }
            }

            var o = new KuduSiteExtensionLogEvent("UnexpectedException");
            o["Error"] = strb.ToString();

            _siteExtensionLogManager.Log(o);
        }

        public void DeprecatedApiUsed(string route, string userAgent, string method, string path)
        {
            var o = new KuduSiteExtensionLogEvent("DeprecatedApiUsed");
            o["route"] = route;
            o["userAgent"] = userAgent;
            o["method"] = method;
            o["path"] = path;

            _siteExtensionLogManager.Log(o);
        }
    }
}