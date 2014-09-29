using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Tracing
{
    public class Analytics : IAnalytics
    {
        private static readonly ConcurrentDictionary<string, string> DeprecatedApiPaths = new ConcurrentDictionary<string, string>();

        private readonly IDeploymentSettingsManager _settings;
        private readonly IServerConfiguration _serverConfiguration;
        private readonly ITraceFactory _traceFactory;

        public Analytics(IDeploymentSettingsManager settings, IServerConfiguration serverConfiguration, ITraceFactory traceFactory)
        {
            _settings = settings;
            _serverConfiguration = serverConfiguration;
            _traceFactory = traceFactory;
        }

        public void ProjectDeployed(string projectType, string result, string error, long deploymentDurationInMilliseconds, string siteMode)
        {
            KuduEventSource.Log.ProjectDeployed(
                _serverConfiguration.ApplicationName,
                NullToEmptyString(projectType),
                NullToEmptyString(result),
                NullToEmptyString(error),
                deploymentDurationInMilliseconds,
                NullToEmptyString(siteMode),
                NullToEmptyString(_settings.GetValue(SettingsKeys.ScmType)));
        }

        public void JobStarted(string jobName, string scriptExtension, string jobType, string siteMode, string error)
        {
            KuduEventSource.Log.WebJobStarted(
                _serverConfiguration.ApplicationName,
                NullToEmptyString(jobName),
                NullToEmptyString(scriptExtension),
                NullToEmptyString(jobType),
                NullToEmptyString(siteMode),
                NullToEmptyString(error));
        }

        public void UnexpectedException(Exception exception, bool trace = true)
        {
            if (trace)
            {
                _traceFactory.GetTracer().TraceError(exception);
            }

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
            path = NullToEmptyString(path);

            // Try not to send the same event (on the same path) more than once.
            if (DeprecatedApiPaths.ContainsKey(path))
            {
                return;
            }

            KuduEventSource.Log.DeprecatedApiUsed(
                _serverConfiguration.ApplicationName,
                NullToEmptyString(route),
                NullToEmptyString(userAgent),
                NullToEmptyString(method),
                path);

            DeprecatedApiPaths[path] = path;
        }

        private static string NullToEmptyString(string s)
        {
            return s ?? String.Empty;
        }
    }
}
