using System.Threading.Tasks;
using Kudu.Contracts.Tracing;

namespace Kudu.Core.Deployment
{
    public interface IAutoSwapHandler
    {
        bool IsAutoSwapEnabled();

        bool IsAutoSwapOngoing();

        Task HandleAutoSwap(string currChangeSetId, ILogger logger, ITracer tracer);
    }
}
