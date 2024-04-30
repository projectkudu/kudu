using System;
using Kudu.Contracts.Infrastructure;

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

        // obfuscate
        public static IDeploymentStatusFile Obfuscate(this IDeploymentStatusFile statusFile)
        {
            return statusFile == null ? statusFile : new ObfuscateDeploymentStatusFile(statusFile);
        }

        public class ObfuscateDeploymentStatusFile : IDeploymentStatusFile
        {
            private readonly IDeploymentStatusFile _statusFile;

            public ObfuscateDeploymentStatusFile(IDeploymentStatusFile statusFile)
            {
                _statusFile = statusFile;
            }

            public string Id { get => _statusFile.Id; set => throw new NotImplementedException(); }
            public DeployStatus Status { get => _statusFile.Status; set => throw new NotImplementedException(); }
            public string StatusText { get => _statusFile.StatusText; set => throw new NotImplementedException(); }
            public string AuthorEmail { get => _statusFile.AuthorEmail.ObfuscateUserName(); set => throw new NotImplementedException(); }
            public string Author { get => _statusFile.Author.ObfuscateUserName(); set => throw new NotImplementedException(); }
            public string Message { get => _statusFile.Message; set => throw new NotImplementedException(); }
            public string Progress { get => _statusFile.Progress; set => throw new NotImplementedException(); }
            public string Deployer { get => _statusFile.Deployer; set => throw new NotImplementedException(); }
            public DateTime ReceivedTime { get => _statusFile.ReceivedTime; set => throw new NotImplementedException(); }
            public DateTime StartTime { get => _statusFile.StartTime; set => throw new NotImplementedException(); }
            public DateTime? EndTime { get => _statusFile.EndTime; set => throw new NotImplementedException(); }
            public DateTime? LastSuccessEndTime { get => _statusFile.LastSuccessEndTime; set => throw new NotImplementedException(); }
            public bool Complete { get => _statusFile.Complete; set => throw new NotImplementedException(); }
            public bool IsTemporary { get => _statusFile.IsTemporary; set => throw new NotImplementedException(); }
            public bool IsReadOnly { get => _statusFile.IsReadOnly; set => throw new NotImplementedException(); }

            public string SiteName => _statusFile.SiteName;

            public string ProjectType { get => _statusFile.ProjectType; set => throw new NotImplementedException(); }
            public string VsProjectId { get => _statusFile.VsProjectId; set => throw new NotImplementedException(); }

            public void Save() => throw new NotImplementedException();
        }
    }
}
