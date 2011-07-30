using System.Collections.Generic;
using Kudu.Core.Infrastructure;
namespace Kudu.Core {
    public interface IEnvironment {
        string RepositoryPath { get; }
        string DeploymentTargetPath { get; }
        string DeploymentCachePath { get; }
        string ApplicationRootPath { get; }
        string AppName { get; }
    }
}
