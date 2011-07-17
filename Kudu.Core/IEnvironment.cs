namespace Kudu.Core {
    public interface IEnvironment {
        bool RequiresBuild { get; }
        string RepositoryPath { get; }
        string DeploymentPath { get; }
    }
}
