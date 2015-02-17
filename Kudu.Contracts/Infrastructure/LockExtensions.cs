using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace Kudu.Contracts.Infrastructure
{
    public static class LockExtensions
    {
        private static readonly TimeSpan _sleepInterval = TimeSpan.FromMilliseconds(250);

        // try acquire lock and then execute the operation
        // return true if lock acquired and operation executed
        public static bool TryLockOperation(this IOperationLock lockObj,
                                         Action operation,
                                         TimeSpan timeout)
        {
            var elapsed = TimeSpan.Zero;

            while (!lockObj.Lock())
            {
                if (elapsed >= timeout)
                {
                    return false;
                }

                Thread.Sleep(_sleepInterval);
                elapsed += _sleepInterval;
            }

            try
            {
                operation();
                return true;
            }
            finally
            {
                lockObj.Release();
            }
        }

        public static async Task<bool> TryLockOperationAsync(this IOperationLock lockObj,
                                               Func<Task> operation,
                                               TimeSpan timeout)
        {
            bool isLocked = await WaitToLockAsync(lockObj, timeout);
            if (!isLocked)
            {
                return false;
            }

            try
            {
                await operation();
                return true;
            }
            finally
            {
                lockObj.Release();
            }
        }

        // acquire lock and then execute the operation
        public static void LockOperation(this IOperationLock lockObj, Action operation, TimeSpan timeout)
        {
            bool success = lockObj.TryLockOperation(operation, timeout);

            if (!success)
            {
                throw new LockOperationException(String.Format(CultureInfo.CurrentCulture, Resources.Error_OperationLockTimeout, timeout.TotalSeconds));
            }
        }

        // acquire lock and then execute the operation
        public static T LockOperation<T>(this IOperationLock lockObj, Func<T> operation, TimeSpan timeout)
        {
            T result = default(T);
            bool success = lockObj.TryLockOperation(() => result = operation(), timeout);

            if (!success)
            {
                throw new LockOperationException(String.Format(CultureInfo.CurrentCulture, Resources.Error_OperationLockTimeout, timeout.TotalSeconds));
            }

            return result;
        }

        public static async Task<T> LockOperationAsync<T>(this IOperationLock lockObj, Func<Task<T>> operation, TimeSpan timeout)
        {
            bool isLocked = await WaitToLockAsync(lockObj, timeout);
            if (!isLocked)
            {
                throw new LockOperationException(String.Format(CultureInfo.CurrentCulture, Resources.Error_OperationLockTimeout, timeout.TotalSeconds));
            }

            try
            {
                return await operation();
            }
            finally
            {
                lockObj.Release();
            }
        }

        private static async Task<bool> WaitToLockAsync(IOperationLock lockObj, TimeSpan timeout)
        {
            var elapsed = TimeSpan.Zero;

            while (!lockObj.Lock())
            {
                if (elapsed >= timeout)
                {
                    return false;
                }

                await Task.Delay(_sleepInterval);
                elapsed += _sleepInterval;
            }

            return true;
        }
    }
}
