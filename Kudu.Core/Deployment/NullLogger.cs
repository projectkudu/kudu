using System.Diagnostics;

namespace Kudu.Core.Deployment
{
    public class NullLogger : ILogger
    {
        public static NullLogger Instance = new NullLogger();

        public TraceLevel TraceLevel
        {
            get { return TraceLevel.Off; }
        }

        public ILogger Log(string value, LogEntryType type)
        {
            return Instance;
        }
    }
}
