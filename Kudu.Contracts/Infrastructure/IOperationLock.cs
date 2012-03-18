using System;

namespace Kudu.Contracts.Infrastructure
{
    public interface IOperationLock
    {
        bool IsHeld { get; }
        bool Lock();
        bool Release();
        bool Wait(TimeSpan timeOut);
    }
}
