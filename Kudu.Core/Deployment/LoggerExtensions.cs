using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Kudu.Core.Deployment
{
    public static class LoggerExtensions
    {
        public static ILogger Log(this ILogger logger, string value)
        {
            return logger.Log(value, LogEntryType.Message);
        }

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
            AggregateException aggregate = exception as AggregateException;
            if (aggregate != null)
            {
                foreach (var inner in aggregate.Flatten().InnerExceptions)
                {
                    returnLog = logger.Log(inner);
                }
            }

            return returnLog;
        }

        public static ILogger LogError(this ILogger logger)
        {
            return logger.Log(String.Empty, LogEntryType.Error);
        }

        public static void LogUnexpectedError(this ILogger logger)
        {
            if (logger == null)
            {
                return;
            }

            logger.Log(Resources.Log_UnexpectedError, LogEntryType.Error);
        }

        public static void LogFileList(this ILogger logger, IEnumerable<string> files)
        {
            logger.Log(String.Join("\n", files.Select(path => String.Format(Resources.Copied, path))));
        }
    }
}