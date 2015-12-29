using System.Threading.Tasks;

namespace Kudu.Core.Deployment
{
    public interface ISiteBuilder
    {
        Task Build(DeploymentContext context);

        void PostBuild(DeploymentContext context);

        string ProjectType { get; }
    }
}
