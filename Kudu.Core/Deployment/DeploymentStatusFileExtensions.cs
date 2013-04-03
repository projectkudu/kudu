using System;

namespace Kudu.Core.Deployment
{
    public static class DeploymentStatusFileExtensions
    {
        public static void MarkFailed(this IDeploymentStatusFile statusFile)
        {
            statusFile.Complete = true;
            statusFile.Status = DeployStatus.Failed;
            statusFile.StatusText = String.Empty;
            statusFile.EndTime = DateTime.UtcNow;
            statusFile.Progress = String.Empty;
            statusFile.Save();
        }

        public static void MarkSuccess(this IDeploymentStatusFile statusFile)
        {
            statusFile.Complete = true;
            statusFile.Status = DeployStatus.Success;
            statusFile.StatusText = String.Empty;
            statusFile.EndTime = DateTime.UtcNow;
            statusFile.LastSuccessEndTime = statusFile.EndTime;
            statusFile.Progress = String.Empty;
            statusFile.Save();
        }

        public static void MarkPending(this IDeploymentStatusFile statusFile)
        {
            if (statusFile.Complete || statusFile.Status != DeployStatus.Pending)
            {
                statusFile.Complete = false;
                statusFile.Status = DeployStatus.Pending;
                statusFile.Save();
            }
        }

        public static void UpdateMessage(this IDeploymentStatusFile statusFile, string message)
        {
            statusFile.Message = message;
            statusFile.Save();
        }

        // best effort
        public static void UpdateProgress(this IDeploymentStatusFile statusFile, string progress)
        {
            try
            {
                // it is unexpected to UpdateProgress while not in Pending/Deploying/Building state
                // instead of throwing, we simply make it more robust by checking status
                if (statusFile.Status == DeployStatus.Pending ||
                    statusFile.Status == DeployStatus.Deploying ||
                    statusFile.Status == DeployStatus.Building)
                {
                    statusFile.Progress = progress;
                    statusFile.Save();
                }
            }
            catch
            {
                // no-op
            }
        }
    }
}
