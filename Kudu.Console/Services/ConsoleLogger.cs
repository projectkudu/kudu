using Kudu.Core.Deployment;

namespace Kudu.Console.Services
{
    public class ConsoleLogger : ILogger
    {
        public bool HasErrors { get; set; }

        public ILogger Log(string value, LogEntryType type)
        {
            if (type == LogEntryType.Error)
            {
                HasErrors = true;

                System.Console.Error.WriteLine(value);
            }
            else
            {
                System.Console.WriteLine(value);
            }

            return NullLogger.Instance;
        }
    }
}
