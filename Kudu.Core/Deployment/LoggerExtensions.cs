using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

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

        public static void LogUnexpetedError(this ILogger logger)
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
