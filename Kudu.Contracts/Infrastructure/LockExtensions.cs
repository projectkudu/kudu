using System;

namespace Kudu.Contracts.Infrastructure
{
    public static class LockExtensions
    {
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
                lockObj.Lock();
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
