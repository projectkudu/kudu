using Kudu.Core.SourceControl;

namespace Kudu.Contracts.SourceControl
{
    public interface IRepositoryFactory
    {
        IRepository EnsureRepository(RepositoryType repositoryType);
        IRepository GetRepository();
        IRepository GetCustomRepository();
        IRepository GetGitRepository();
    }
}
