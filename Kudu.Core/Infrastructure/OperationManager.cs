using System;
using System.Threading;
using System.Threading.Tasks;
using Kudu.Core.Tracing;

namespace Kudu.Core.Infrastructure
{
    public static class OperationManager
    {
        private const int DefaultRetries = 3;
        private const int DefaultDelayBeforeRetry = 250; // 250 ms

        public static void Attempt(Action action, int retries = DefaultRetries, int delayBeforeRetry = DefaultDelayBeforeRetry)
        {
            OperationManager.Attempt<object>(() =>
            {
                action();
                return null;
            }, retries, delayBeforeRetry);
        }

        public static T Attempt<T>(Func<T> action, int retries = DefaultRetries, int delayBeforeRetry = DefaultDelayBeforeRetry, Func<Exception, bool> shouldRetry = null)
        {
            T result = default(T);

            while (retries > 0)
            {
                try
                {
                    result = action();
                    break;
                }
                catch (Exception ex)
                {
                    if (shouldRetry != null && !shouldRetry(ex))
                    {
                        throw;
                    }

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

        public static Task AttemptAsync(Func<Task> action, int retries = DefaultRetries, int delayBeforeRetry = DefaultDelayBeforeRetry)
        {
            return AttemptAsync(async () =>
            {
                await action();
                return true;
            }, retries, delayBeforeRetry);
        }

        public static async Task<TVal> AttemptAsync<TVal>(Func<Task<TVal>> action, int retries = DefaultRetries, int delayBeforeRetry = DefaultDelayBeforeRetry)
        {
            while (retries > 0)
            {
                try
                {
                    return await action();
                }
                catch
                {
                    retries--;
                    if (retries == 0)
                    {
                        throw;
                    }
                }

                await Task.Delay(delayBeforeRetry);
            }

            return default(TVal);
        }

        public static void SafeExecute(Action action)
        {
            try
            {
                action();
            }
            catch
            {
                // no-op
            }
        }

        public static T SafeExecute<T>(Func<T> action)
        {
            try
            {
                return action();
            }
            catch
            {
                return default(T);
            }
        }

        public static void CriticalExecute(string method, Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                KuduEventSource.Log.KuduException(
                    ServerConfiguration.GetApplicationName(),
                    method,
                    string.Empty,
                    string.Empty,
                    "Critical execution encounters an exception and will fail fast in 60s",
                    ex.ToString());

                ThreadPool.QueueUserWorkItem(_ =>
                {
                    Thread.Sleep(60000);
                    System.Environment.FailFast($"Method {method} encounters critical exception {ex}");
                });

                throw;
            }
        }
    }
}
