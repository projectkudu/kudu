namespace Kudu.Core.Deployment
{
    public enum FetchDeploymentRequestResult
    {
        Unknown = 0,
        ForbiddenScmDisabled,
        RunningAsynchronously,
        ConflictAutoSwapOngoing,
        RanSynchronously,
        Pending,
        ConflictDeploymentInProgress,
        ConflictRunFromRemoteZipConfigured
    }
}
