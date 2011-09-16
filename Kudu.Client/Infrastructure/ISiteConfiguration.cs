using Kudu.Core.Deployment;
using Kudu.Core.Editor;
using Kudu.Core.SourceControl;

namespace Kudu.Client.Infrastructure {
    public interface ISiteConfiguration {
        string Name { get; }
        string ServiceUrl { get; }
        string SiteUrl { get; }

        IEditorFileSystem FileSystem { get; }
        IDeploymentManager DeploymentManager { get; }
        IRepositoryManager RepositoryManager { get; }
        IRepository Repository { get; }
    }
}