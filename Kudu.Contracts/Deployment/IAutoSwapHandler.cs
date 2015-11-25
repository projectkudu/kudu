namespace Kudu.Core.Deployment
{
    public interface IAutoSwapHandler
    {
        bool IsAutoSwapEnabled();

        bool IsAutoSwapOngoing();

        void HandleAutoSwap(bool verifyActiveDeploymentIdChanged);
    }
}
