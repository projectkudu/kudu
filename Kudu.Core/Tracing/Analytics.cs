using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
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

        public void JobStarted(string jobName, string scriptExtension, string jobType, string siteMode, string error, string trigger)
        {
            KuduEventSource.Log.WebJobStarted(
                _serverConfiguration.ApplicationName,
                NullToEmptyString(jobName),
                NullToEmptyString(scriptExtension),
                NullToEmptyString(jobType),
                NullToEmptyString(siteMode),
                NullToEmptyString(error),
                NullToEmptyString(trigger));
        }

        public void UnexpectedException(Exception exception, bool trace = true)
        {
            KuduEventSource.Log.KuduUnexpectedException(
                _serverConfiguration.ApplicationName,
                GetExceptionContent(exception, trace));
        }

        public void UnexpectedException(Exception ex, string method, string path, string result, string message, bool trace = true)
        {
            // ETW event for KuduException is not existed yet in current Antares deployment
            // "duplicate" log with KuduUnexpectedException so that we will have some data for trouble shooting for now
            // TODO: once next antares release is out, KuduUnexpectedException will be merge into KuduException
            KuduEventSource.Log.KuduUnexpectedException(
                 _serverConfiguration.ApplicationName,
                 string.Format(CultureInfo.InvariantCulture, "Method: {0}, Path: {1}, Result: {2}, Message: {3}, Exception: {4}",
                     NullToEmptyString(method),
                     NullToEmptyString(path),
                     NullToEmptyString(result),
                     NullToEmptyString(message),
                     GetExceptionContent(ex, trace)));

            KuduEventSource.Log.KuduException(
                _serverConfiguration.ApplicationName,
                NullToEmptyString(method),
                NullToEmptyString(path),
                NullToEmptyString(result),
                NullToEmptyString(message),
                GetExceptionContent(ex, trace));
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

        public void SiteExtensionEvent(string method, string path, string result, string deploymentDurationInMilliseconds, string message)
        {
            KuduEventSource.Log.KuduSiteExtensionEvent(
                _serverConfiguration.ApplicationName,
                NullToEmptyString(method),
                NullToEmptyString(path),
                NullToEmptyString(result),
                NullToEmptyString(deploymentDurationInMilliseconds),
                NullToEmptyString(message));
        }

        private static string NullToEmptyString(string s)
        {
            return s ?? String.Empty;
        }

        private string GetExceptionContent(Exception exception, bool trace)
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

            return strb.ToString();
        }
    }
}
