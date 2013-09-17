using System.IO.Abstractions;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;

namespace Kudu.Core.Tracing
{
    public class Analytics : IAnalytics
    {
        private readonly SiteExtensionLogManager _siteExtensionLogManager;
        private readonly IDeploymentSettingsManager _settings;

        public Analytics(IDeploymentSettingsManager settings, IFileSystem fileSystem, ITracer tracer, string directoryPath)
        {
            _settings = settings;
            _siteExtensionLogManager = new SiteExtensionLogManager(fileSystem, tracer, directoryPath);
        }

        public void ProjectDeployed(string projectType, string result, string error, long deploymentDurationInMilliseconds)
        {
            var o = new SiteDeployedSiteExtensionLogEvent()
            {
                SiteType = projectType,
                ScmType = _settings.GetValue(SettingsKeys.ScmType),
                Result = result,
                Error = error,
                Latency = deploymentDurationInMilliseconds
            };

            _siteExtensionLogManager.Log(o);
        }

        private class SiteDeployedSiteExtensionLogEvent : SiteExtensionLogEvent
        {
            public string SiteType
            {
                set { this["SiteType"] = value; }
            }

            public string ScmType
            {
                set { this["ScmType"] = value; }
            }

            public string Result
            {
                set { this["Result"] = value; }
            }

            public string Error
            {
                set { this["Error"] = value; }
            }

            public long? Latency
            {
                set { this["Latency"] = value; }
            }

            public SiteDeployedSiteExtensionLogEvent()
                : base("Kudu", "SiteDeployed")
            {
            }
        }
    }
}
