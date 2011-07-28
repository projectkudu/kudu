using System;

namespace Kudu.Core.Deployment {
    public static class LoggerExtensions {
        public static void Log(this ILogger logger, string value, params object[] args) {
            logger.Log(String.Format(value, args));
        }
    }
}
