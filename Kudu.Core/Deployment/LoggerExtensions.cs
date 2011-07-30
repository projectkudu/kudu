using System;

namespace Kudu.Core.Deployment {
    public static class LoggerExtensions {
        public static void Log(this ILogger logger, string value, params object[] args) {
            logger.Log(String.Format(value, args));
        }

        public static void Log(this ILogger logger, Exception exception) {
            logger.Log(exception.Message);
#if DEBUG
            logger.Log(exception.StackTrace);
#endif
        }
    }
}
