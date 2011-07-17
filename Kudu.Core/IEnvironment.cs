using System.Collections.Generic;
namespace Kudu.Core {
    public interface IEnvironment {
        // REVIEW: Do we still need this?
        bool RequiresBuild { get; }
        IEnumerable<string> GetWebApplicationProjects();
        string RepositoryPath { get; }
        string DeploymentPath { get; }
    }
}
