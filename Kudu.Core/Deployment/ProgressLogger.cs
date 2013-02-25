using System;

namespace Kudu.Core.Deployment
{
    public class ProgressLogger : ILogger
    {
        private readonly string _id;
        private readonly IDeploymentStatusManager _status;
        private readonly ILogger _innerLogger;

        public ProgressLogger(string id, IDeploymentStatusManager status, ILogger innerLogger)
        {
            _id = id;
            _status = status;
            _innerLogger = innerLogger;
        }

        public ILogger Log(string value, LogEntryType type)
        {
            IDeploymentStatusFile statusFile = _status.Open(_id);
            if (statusFile != null)
            {
                statusFile.UpdateProgress(value);
            }

            // No need to wrap this as we only support top-level progress
            return _innerLogger.Log(value, type);
        }
    }
}