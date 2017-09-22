namespace Kudu.Services.ServiceHookHandlers
{
    public enum DeploymentResponse
    {
        Unknown = 0,
        ForbiddenScmDisabled,
        RunningInBackground,
        AutoSwapOngoing,
        RanSynchronously,
        AcceptedAndPending
    }
}
