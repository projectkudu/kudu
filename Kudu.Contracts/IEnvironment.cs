using Kudu.Core.SourceControl;
namespace Kudu.Core
{
    public interface IEnvironment
    {
        string RepositoryPath { get; }
        string DeploymentRepositoryPath { get; }
        string DeploymentTargetPath { get; }
        string DeploymentCachePath { get; }
        string ApplicationRootPath { get; }
        string TempPath { get; }
        string AppName { get; }
    }
}
