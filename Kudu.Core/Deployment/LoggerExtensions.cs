using System;
using System.Globalization;

namespace Kudu.Core.Deployment
{
    public static class LoggerExtensions
    {
        public static ILogger Log(this ILogger logger, string value, params object[] args)
        {
            return logger.Log(String.Format(CultureInfo.CurrentCulture, value, args), LogEntryType.Message);
        }

        public static ILogger Log(this ILogger logger, Exception exception)
        {
            var returnLog = logger.Log(exception.Message, LogEntryType.Error);
#if DEBUG
            logger.Log(exception.StackTrace, LogEntryType.Error);
#endif
            return returnLog;
        }

        public static string GetTopLevelError(this ILogger logger)
        {
            // TODO: Clean this up
            if (logger is XmlLogger)
            {
                return ((XmlLogger)logger).GetFirstErrorEntryMessage();
            }

            return "Cannot parse error from this log format. Expand the log to get details of the error.";
        }
    }
}
