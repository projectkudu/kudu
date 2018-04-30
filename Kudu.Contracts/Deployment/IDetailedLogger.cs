using System.Collections.Generic;

namespace Kudu.Core.Deployment
{
    public interface IDetailedLogger : ILogger
    {
        IEnumerable<LogEntry> GetLogEntries();
        IEnumerable<LogEntry> GetLogEntryDetails(string entryId);
    }
}
