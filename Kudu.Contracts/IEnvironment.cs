using Kudu.Core.SourceControl;
namespace Kudu.Core
{
    public interface IEnvironment
    {
        string RootPath { get; }                // e.g. /
        string SiteRootPath { get; }            // e.g. /site
        string RepositoryPath { get; }          // e.g. /site/repository
        string WebRootPath { get; }             // e.g. /site/wwwroot
        string DeploymentCachePath { get; }     // e.g. /site/deployments
        string SSHKeyPath { get; }
        string NuGetCachePath { get; }
        string TempPath { get; }
        string ScriptPath { get; }
        string TracePath { get; }               // e.g. /logfiles/git/trace
        string DeploymentTracePath { get; }     // e.g. /logfiles/git/deployment
    }
}
