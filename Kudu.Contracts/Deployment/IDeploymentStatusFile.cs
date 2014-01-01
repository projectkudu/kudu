using System;

namespace Kudu.Core.Deployment
{
    public interface IDeploymentStatusFile
    {
        string Id { get; set; }
        DeployStatus Status { get; set; }
        string StatusText { get; set; }
        string AuthorEmail { get; set; }
        string Author { get; set; }
        string Message { get; set; }
        string Progress { get; set; }
        string Deployer { get; set; }
        DateTime ReceivedTime { get; set; }
        DateTime StartTime { get; set; }
        DateTime? EndTime { get; set; }
        DateTime? LastSuccessEndTime { get; set; }
        bool Complete { get; set; }
        bool IsTemporary { get; set; }
        bool IsReadOnly { get; set; }
        string SiteName { get; }

        void Save();
    }
}