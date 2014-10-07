namespace Kudu.Core.Deployment
{
    public interface IAutoSwapHandler
    {
        bool IsAutoSwapOngoing();

        void HandleAutoSwap(bool verifyActiveDeploymentIdChanged);
    }
}
