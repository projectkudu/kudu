using Kudu.Core.SourceControl;
namespace Kudu.Core
{
    public interface IEnvironment
    {
        string RootPath { get; }
        string RepositoryPath { get; }
        string WebRootPath { get; }
        string DeploymentCachePath { get; }
        string SSHKeyPath { get; }
        string SiteRootPath { get; }
        string NuGetCachePath { get; }
        string TempPath { get; }
        string ScriptPath { get; }
    }
}
