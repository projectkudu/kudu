namespace Kudu.Contracts.Infrastructure
{
    public interface IOperationLock
    {
        bool Lock();
        bool Release();
    }
}
