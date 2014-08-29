using System;
using System.Net.Http;
using System.Net.Http.Headers;

namespace Kudu.Core.Tracing
{
    public interface IAnalytics
    {
        void ProjectDeployed(string projectType, string result, string error, long deploymentDurationInMilliseconds, string siteMode);

        void JobStarted(string jobName, string scriptExtension, string jobType, string siteMode, string error);

        void UnexpectedException(Exception ex);

        void DeprecatedApiUsed(string route, string userAgent, string method, string path);
    }
}
