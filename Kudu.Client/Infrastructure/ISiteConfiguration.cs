using Kudu.Core.Commands;
using Kudu.Core.Deployment;
using Kudu.Core.Editor;
using Kudu.Core.SourceControl;

namespace Kudu.Client.Infrastructure {
    public interface ISiteConfiguration {
        string Name { get; }
        string ServiceUrl { get; }
        string SiteUrl { get; }

        IEditorFileSystem FileSystem { get; }
        IEditorFileSystem DevFileSystem { get; }
        IDeploymentManager DeploymentManager { get; }
        IRepository Repository { get; }
        ICommandExecutor CommandExecutor { get; }
        ICommandExecutor DevCommandExecutor { get; }
    }
}