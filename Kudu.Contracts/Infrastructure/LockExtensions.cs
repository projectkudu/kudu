using System;
using System.Threading;

namespace Kudu.Contracts.Infrastructure
{
    public static class LockExtensions
    {

        public static void LockOrWait(this IOperationLock lockObj,
                                      Action operation,
                                      TimeSpan timeOut)
        {
            LockOperation(lockObj, operation, () => lockObj.Wait(timeOut));
        }

        public static void LockOperation(this IOperationLock lockObj,
                                         Action operation,
                                         Action onBusy)
        {
            bool lockTaken = lockObj.Lock();

            if (!lockTaken)
            {
                onBusy();
                return;
            }

            try
            {
                operation();
            }
            finally
            {
                if (lockTaken)
                {
                    lockObj.Release();
                }
            }
        }

        // try acquire lock and then execute the operation
        // return true if lock acquired and operation executed
        public static bool TryLockOperation(this IOperationLock lockObj,
                                         Action operation,
                                         TimeSpan timeout)
        {
            var interval = TimeSpan.FromMilliseconds(250);
            var elapsed = TimeSpan.Zero;

            while (!lockObj.Lock())
            {
                if (elapsed >= timeout)
                {
                    return false;
                }

                Thread.Sleep(interval);
                elapsed += interval;
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

        // acquire lock and then execute the operation
        public static void LockOperation(this IOperationLock lockObj, Action operation, TimeSpan timeout)
        {
            bool success = lockObj.TryLockOperation(operation, timeout);

            if (!success)
            {
                throw new InvalidOperationException("Unable to acquire lock within a given time.");
            }
        }

        // acquire lock and then execute the operation
        public static T LockOperation<T>(this IOperationLock lockObj, Func<T> operation, TimeSpan timeout)
        {
            T result = default(T);
            bool success = lockObj.TryLockOperation(() => result = operation(), timeout);

            if (!success)
            {
                throw new InvalidOperationException("Unable to acquire lock within a given time.");
            }

            return result;
        }
    }
}
