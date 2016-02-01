using System.Threading.Tasks;

namespace Kudu.Core.Deployment
{
    public interface IAutoSwapHandler
    {
        bool IsAutoSwapEnabled();

        bool IsAutoSwapOngoing();

        Task HandleAutoSwap(string currChangeSetId, DeploymentContext context);
    }
}
