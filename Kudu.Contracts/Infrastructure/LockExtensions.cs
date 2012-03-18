using System;

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
    }
}
