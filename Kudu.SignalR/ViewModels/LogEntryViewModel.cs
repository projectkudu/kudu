using System;
using Kudu.Core.Deployment;

namespace Kudu.SignalR.ViewModels
{
    public class LogEntryViewModel
    {
        public LogEntryViewModel(LogEntry entry)
        {
            LogTime = entry.LogTime;
            Message = entry.Message;
            Type = entry.Type;
        }

        public LogEntryType Type { get; set; }
        public DateTime LogTime { get; private set; }
        public string Message { get; private set; }
    }
}