using System.Threading.Tasks;

namespace Kudu.Core.Deployment
{
    public interface ISiteBuilder
    {
        Task Build(DeploymentContext context);
        string ProjectType { get; }
    }
}
