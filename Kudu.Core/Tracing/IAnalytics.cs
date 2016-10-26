using System;
using System.Runtime.CompilerServices;

namespace Kudu.Core.Tracing
{
    public interface IAnalytics
    {
        void ProjectDeployed(string projectType, string result, string error, long deploymentDurationInMilliseconds, string siteMode, string vsProjectId = "");

        void JobStarted(string jobName, string scriptExtension, string jobType, string siteMode, string error, string trigger);

        void JobEvent(string jobName, string message, string jobType, string error);

        void UnexpectedException(Exception ex, bool trace = true, [CallerMemberName] string memberName = null, [CallerFilePath] string sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = 0);

        void UnexpectedException(Exception ex, string method, string path, string result, string message, bool trace = true);

        void DeprecatedApiUsed(string route, string userAgent, string method, string path);

        void SiteExtensionEvent(string method, string path, string result, string deploymentDurationInMilliseconds, string Message);
    }
}
