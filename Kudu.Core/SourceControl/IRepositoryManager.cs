namespace Kudu.Core.SourceControl {
    public interface IRepositoryManager {
        void CreateRepository(RepositoryType type);
        IRepository GetRepository();
        RepositoryType GetRepositoryType();
        void Delete();
    }
}
