namespace Kudu.Core.Deployment
{
    public enum FetchDeploymentRequestResult
    {
        Unknown = 0,
        ForbiddenScmDisabled,
        RunningInBackground,
        AutoSwapOngoing,
        RanSynchronously,
        AcceptedAndPending
    }
}
