using System;

namespace Kudu.Core.Deployment
{
    public interface IProgressReporter
    {
        void ReportProgress(string statusText);
    }
}
