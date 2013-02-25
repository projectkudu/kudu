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
            statusFile.EndTime = DateTime.Now;
            statusFile.Progress = String.Empty;
            statusFile.Save();
        }

        public static void MarkSuccess(this IDeploymentStatusFile statusFile)
        {
            statusFile.Complete = true;
            statusFile.Status = DeployStatus.Success;
            statusFile.StatusText = String.Empty;
            statusFile.EndTime = DateTime.Now;
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

        public static void UpdateProgress(this IDeploymentStatusFile statusFile, string progress)
        {
            statusFile.Progress = progress;
            statusFile.Save();
        }
    }
}
