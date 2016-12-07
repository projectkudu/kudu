using System;
using System.Collections.Concurrent;
using System.Text;
using Kudu.Contracts.Settings;
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

        public void ProjectDeployed(string projectType, string result, string error, long deploymentDurationInMilliseconds, string siteMode, string vsProjectId = "")
        {
            KuduEventSource.Log.ProjectDeployed(
                _serverConfiguration.ApplicationName,
                NullToEmptyString(projectType),
                NullToEmptyString(result),
                NullToEmptyString(error),
                deploymentDurationInMilliseconds,
                NullToEmptyString(siteMode),
                NullToEmptyString(_settings.GetValue(SettingsKeys.ScmType)),
                NullToEmptyString(vsProjectId));
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

        public void JobEvent(string jobName, string message, string jobType, string error)
        {
            KuduEventSource.Log.WebJobEvent(
                _serverConfiguration.ApplicationName,
                NullToEmptyString(jobName),
                NullToEmptyString(message),
                NullToEmptyString(jobType),
                NullToEmptyString(error));
        }

        public void UnexpectedException(Exception exception, bool trace = true, string memberName = null, string sourceFilePath = null, int sourceLineNumber = 0)
        {
            // this happen during unexpected situation and should not throw (masking the original exception)
            OperationManager.SafeExecute(() =>
            {
                if (exception.AbortedByKudu())
                {
                    return;
                }

                KuduEventSource.Log.KuduException(
                    _serverConfiguration.ApplicationName,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    GetExceptionContent(exception, trace, memberName, sourceFilePath, sourceLineNumber));
            });
        }

        public void UnexpectedException(Exception ex, string method, string path, string result, string message, bool trace = true)
        {
            // this happen during unexpected situation and should not throw (masking the original exception)
            OperationManager.SafeExecute(() =>
            {
                if (ex.AbortedByKudu())
                {
                    return;
                }

                KuduEventSource.Log.KuduException(
                    _serverConfiguration.ApplicationName,
                    NullToEmptyString(method),
                    NullToEmptyString(path),
                    NullToEmptyString(result),
                    NullToEmptyString(message),
                    GetExceptionContent(ex, trace));
            });
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

        private string GetExceptionContent(Exception exception, bool trace, string memberName = null, string sourceFilePath = null, int sourceLineNumber = 0)
        {
            string methodInfo = null;
            if (!String.IsNullOrEmpty(memberName))
            {
                methodInfo = String.Format("{0}() at {1}:{2}", memberName, sourceFilePath, sourceLineNumber);
            }

            if (trace)
            {
                // best effort to handle file system failure.
                OperationManager.SafeExecute(() => _traceFactory.GetTracer().TraceError(exception, "{0}", methodInfo));
            }

            var strb = new StringBuilder();
            if (!String.IsNullOrEmpty(methodInfo))
            {
                strb.AppendLine(methodInfo);
            }
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
