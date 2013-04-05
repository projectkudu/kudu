using System;

namespace Kudu.Contracts.Infrastructure
{
    public interface IOperationLock
    {
        bool IsHeld { get; }
        bool Lock();
        void Release();
    }
}
