using System;

namespace Kudu.Core.Deployment
{
    internal class ProgressReporter : IProgressReporter
    {
        private readonly Action<string> _progressAction;

        public ProgressReporter(Action<string> progressAction)
        {
            _progressAction = progressAction;
        }

        public void ReportProgress(string statusText)
        {
            _progressAction(statusText);
        }
    }
}
