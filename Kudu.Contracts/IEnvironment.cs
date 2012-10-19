using Kudu.Core.SourceControl;
namespace Kudu.Core
{
    public interface IEnvironment
    {
        string DeploymentRepositoryPath { get; }
        string DeploymentTargetPath { get; }
        string DeploymentCachePath { get; }
        string SSHKeyPath { get; }
        string ApplicationRootPath { get; }
        string NuGetCachePath { get; }
        string TempPath { get; }
        string ScriptPath { get; }
    }
}
