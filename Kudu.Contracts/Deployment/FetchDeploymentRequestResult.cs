namespace Kudu.Core.Deployment
{
    public enum FetchDeploymentRequestResult
    {
        Unknown = 0,
        ForbiddenScmDisabled,
        RunningAynschronously,
        ConflictAutoSwapOngoing,
        RanSynchronously,
        Pending,
        ConflictDeploymentInProgress
    }
}
