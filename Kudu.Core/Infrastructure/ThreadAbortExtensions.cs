using System;
using System.Threading;

namespace Kudu.Core
{
    public static class ThreadAbortExtensions
    {
        public const string KuduThreadAbortMessage = "Kudu aborts thread.";

        public static void KuduAbort(this Thread thread, string message)
        {
            thread.Abort(String.Format("{0}  {1}", KuduThreadAbortMessage, message));
        }

        public static bool AbortedByKudu(this Exception exception)
        {
            return AbortedByKudu(exception as ThreadAbortException);
        }

        public static bool AbortedByKudu(this ThreadAbortException exception)
        {
            var state = exception?.ExceptionState as string;
            return !String.IsNullOrEmpty(state) && state.StartsWith(KuduThreadAbortMessage, StringComparison.OrdinalIgnoreCase);
        }

        public static string GetString(this ThreadAbortException exception)
        {
            if (exception.ExceptionState != null)
            {
                return String.Format("{0}  {1}", exception.ExceptionState, exception);
            }

            return exception.ToString();
        }
    }
}
