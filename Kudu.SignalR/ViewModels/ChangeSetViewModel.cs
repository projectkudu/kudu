using System;
using System.IO;
using Kudu.Core.SourceControl;
using Kudu.SignalR.Infrastructure;

namespace Kudu.SignalR.ViewModels
{
    public class ChangeSetViewModel
    {
        public string Id { get; set; }
        public string ShortId { get; set; }
        public string AuthorName { get; set; }
        public string EmailHash { get; set; }
        public string Date { get; set; }
        public string Message { get; set; }
        public string Summary { get; set; }
        public bool Active { get; set; }

        public ChangeSetViewModel(ChangeSet changeSet)
        {
            Id = changeSet.Id;
            ShortId = changeSet.Id.Substring(0, 12);
            AuthorName = changeSet.AuthorName;
            EmailHash = String.IsNullOrEmpty(changeSet.AuthorEmail) ? null : HelperMethods.Hash(changeSet.AuthorEmail);
            Date = changeSet.Timestamp.ToString("u");
            Message = Process(changeSet.Message);
            // Show first line only
            var reader = new StringReader(changeSet.Message);
            Summary = Process(Trim(reader.ReadLine(), 300));
        }

        private string Trim(string value, int max)
        {
            if (String.IsNullOrEmpty(value))
            {
                return value;
            }

            if (value.Length > max)
            {
                return value.Substring(0, max) + "...";
            }
            return value;
        }

        private string Process(string value)
        {
            if (String.IsNullOrEmpty(value))
            {
                return value;
            }
            return value.Trim().Replace("\n", "<br/>");
        }
    }
}