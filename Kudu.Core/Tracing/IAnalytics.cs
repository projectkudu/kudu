using System;

namespace Kudu.Core.Tracing
{
    public interface IAnalytics
    {
        void ProjectDeployed(string projectType, string result, string error, long deploymentDurationInMilliseconds, string siteMode);
        void JobStarted(string jobName, string scriptExtension, string jobType, string siteMode);
        void UnexpectedException(Exception ex);
    }
}
