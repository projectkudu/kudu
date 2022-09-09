namespace Kudu.Contracts.Scan
{
    public enum ScanStatus
    {
        Starting,
        Executing,
        Failed,
        TimeoutFailure,
        Success,
        ForceStopped
    }
}
