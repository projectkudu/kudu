using System;
using System.Threading;

namespace Kudu.Core.Infrastructure
{
    internal static class OperationManager
    {
        public static void Attempt(Action action, Func<bool> condition = null, int retries = 3, int delayBeforeRetry = 250)
        {
            OperationManager.Attempt<object>(() =>
            {
                action();
                return null;
            });
        }

        public static T Attempt<T>(Func<T> action, Func<bool> condition = null, int retries = 3, int delayBeforeRetry = 250)
        {
            T result = default(T);

            while (retries > 0)
            {
                try
                {
                    result = action();
                    break;
                }
                catch
                {
                    retries--;
                    if (retries == 0)
                    {
                        throw;
                    }
                }

                Thread.Sleep(delayBeforeRetry);
            }

            return result;
        }

        public static void WaitUntil(Func<bool> condition, int retries = 3, int delayBeforeRetry = 500)
        {
            if (condition())
            {
                return;
            }

            while (retries > 0)
            {
                if (condition())
                {
                    return;
                }

                retries--; 
                Thread.Sleep(delayBeforeRetry);
            }
        }

    }
}
