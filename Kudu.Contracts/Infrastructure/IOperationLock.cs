using System;
using System.Threading.Tasks;

namespace Kudu.Contracts.Infrastructure
{
    public interface IOperationLock
    {
        bool IsHeld { get; }
        OperationLockInfo LockInfo { get; }
        bool Lock(string operationName);

        // Waits until lock can be acquired after which the task completes.
        Task LockAsync(string operationName);
        void Release();
    }
}
