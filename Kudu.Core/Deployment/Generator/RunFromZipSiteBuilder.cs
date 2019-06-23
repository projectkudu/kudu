using System.Threading.Tasks;

namespace Kudu.Core.Deployment.Generator
{
    public class RunFromZipSiteBuilder : ISiteBuilder
    {
        public string ProjectType => "Run-From-Zip";

        public Task Build(DeploymentContext context)
        {
            // no-op
            context.Logger.Log($"Skipping build. Project type: {ProjectType}");
            return Task.CompletedTask;
        }

        public void PostBuild(DeploymentContext context)
        {
            // no-op
            context.Logger.Log($"Skipping post build. Project type: {ProjectType}");
        }
    }
}
