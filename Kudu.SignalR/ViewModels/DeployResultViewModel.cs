using System;
using Kudu.Core.Deployment;
using Kudu.SignalR.Infrastructure;

namespace Kudu.SignalR.ViewModels
{
    public class DeployResultViewModel
    {
        public DeployResultViewModel(DeployResult result)
        {
            Id = result.Id;
            ShortId = result.Id.Substring(0, 10);
            Message = result.Message;
            Author = result.Author;
            EmailHash = String.IsNullOrEmpty(result.AuthorEmail) ? null : HelperMethods.Hash(result.AuthorEmail); ;
            Status = result.Status;
            DisplayStatus = result.Status.ToString();
            StatusText = result.StatusText;
            DeployEndTime = result.DeployEndTime;
            DeployStartTime = result.DeployStartTime;
            Percentage = result.Percentage;
        }

        public string Id { get; set; }
        public string ShortId { get; set; }
        public string StatusText { get; set; }
        public string DisplayStatus { get; set; }
        public DeployStatus Status { get; set; }
        public string Message { get; set; }
        public string Author { get; set; }
        public string EmailHash { get; set; }
        public DateTime DeployStartTime { get; set; }
        public DateTime? DeployEndTime { get; set; }
        public bool Current { get; set; }
        public int Percentage { get; set; }
        public string FailureMessage { get; set; }
    }
}