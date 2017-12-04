namespace Kudu.Core.Deployment
{
    public class FetchDeploymentRequestResult
    {
        public FetchDeploymentRequestResult(FetchDeploymentRequestResultStatus status, string statusText = null)
        {
            Status = status;
            StatusText = statusText ?? string.Empty;
        }
        public FetchDeploymentRequestResultStatus Status { get; }
        public string StatusText { get; }
    }
    
    public enum FetchDeploymentRequestResultStatus
    {
        Unknown = 0,
        ForbiddenScmDisabled,
        RunningAynschronously,
        ConflictAutoSwapOngoing,
        RanSynchronously,
        Pending,
        ConflictDeploymentInProgress,
        RanSynchronouslyFailed
    }
}
