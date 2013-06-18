using System.Threading.Tasks;

namespace Kudu.Contracts.Infrastructure
{
    public interface IOperationLock
    {
        bool IsHeld { get; }
        bool Lock();

        // Waits until lock can be acquired after which the task completes.
        Task LockAsync();
        void Release();
    }
}
