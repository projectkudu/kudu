using System;
using Kudu.Core.Deployment;

namespace Kudu.SignalR.ViewModels
{
    public class DeployResultViewModel
    {
        public DeployResultViewModel(DeployResult result)
        {
            Id = result.Id;
            ShortId = result.Id.Substring(0, 8);
            Message = result.Message;
            Author = result.Author;
            Status = result.Status.ToString();
            StatusText = result.StatusText;
            DeployEndTime = result.DeployEndTime;
            DeployStartTime = result.DeployStartTime;
        }

        public string Id { get; set; }
        public string ShortId { get; set; }
        public string StatusText { get; set; }
        public string Status { get; set; }
        public string Message { get; set; }
        public string Author { get; set; }
        public DateTime DeployStartTime { get; set; }
        public DateTime? DeployEndTime { get; set; }
        public bool Active { get; set; }
    }
}