namespace Kudu.Contracts.Scan
{
    public enum ScanRequestResult
    {
        RunningAynschronously,
        RanSynchronously,
        Pending,
        AsyncScanFailed,
        NoFileModifications,
        ScanAlreadyInProgress
    }
}
