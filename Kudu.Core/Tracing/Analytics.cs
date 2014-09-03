using System;
using System.Text;
using Kudu.Contracts.Settings;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Tracing
{
    public class Analytics : IAnalytics
    {
        private readonly IDeploymentSettingsManager _settings;
        private readonly IServerConfiguration _serverConfiguration;

        public Analytics(IDeploymentSettingsManager settings, IServerConfiguration serverConfiguration)
        {
            _settings = settings;
            _serverConfiguration = serverConfiguration;
        }

        public void ProjectDeployed(string projectType, string result, string error, long deploymentDurationInMilliseconds, string siteMode)
        {
            KuduEventSource.Log.ProjectDeployed(_serverConfiguration.ApplicationName, projectType, result, error, deploymentDurationInMilliseconds, siteMode, _settings.GetValue(SettingsKeys.ScmType));
        }

        public void JobStarted(string jobName, string scriptExtension, string jobType, string siteMode, string error)
        {
            KuduEventSource.Log.WebJobStarted(_serverConfiguration.ApplicationName, jobName, scriptExtension, jobType, siteMode, error);
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

            KuduEventSource.Log.KuduUnexpectedException(_serverConfiguration.ApplicationName, strb.ToString());
        }

        public void DeprecatedApiUsed(string route, string userAgent, string method, string path)
        {
            KuduEventSource.Log.DeprecatedApiUsed(_serverConfiguration.ApplicationName, route, userAgent, method, path);
        }
    }
}
