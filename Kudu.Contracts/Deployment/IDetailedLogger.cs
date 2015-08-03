using Kudu.Core.Deployment;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kudu.Core.Deployment
{
    public interface IDetailedLogger : ILogger
    {
        IEnumerable<LogEntry> GetLogEntries();
        IEnumerable<LogEntry> GetLogEntryDetails(string entryId);
    }
}
