using System;
using System.IO.Abstractions;
using System.Text;
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

        public void JobStarted(string jobName, string scriptExtension, string jobType)
        {
            var o = new JobStartedSiteExtensionLogEvent()
            {
                JobName = jobName,
                ScriptExtension = scriptExtension,
                JobType = jobType
            };

            _siteExtensionLogManager.Log(o);
        }

        public void UnexpectedException(Exception exception)
        {
            var strb = new StringBuilder();
            strb.AppendLine(exception.ToString());

            AggregateException aggregate = exception as AggregateException;
            if (aggregate != null)
            {
                foreach (var inner in aggregate.Flatten().InnerExceptions)
                {
                    strb.AppendLine(inner.ToString());
                }
            }

            var o = new UnexpectedExceptionSiteExtensionLogEvent()
            {
                Error = strb.ToString()
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

        private class JobStartedSiteExtensionLogEvent : SiteExtensionLogEvent
        {
            public string JobName
            {
                set { this["JobName"] = value; }
            }

            public string ScriptExtension
            {
                set { this["ScriptExtension"] = value; }
            }

            public string JobType
            {
                set { this["JobType"] = value; }
            }

            public JobStartedSiteExtensionLogEvent()
                : base("Kudu", "JobStarted")
            {
            }
        }

        private class UnexpectedExceptionSiteExtensionLogEvent : SiteExtensionLogEvent
        {
            public string Error
            {
                set { this["Error"] = value; }
            }

            public UnexpectedExceptionSiteExtensionLogEvent()
                : base("Kudu", "UnexpectedException")
            {
            }
        }
    }
}
