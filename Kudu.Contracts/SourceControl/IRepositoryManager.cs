namespace Kudu.Core.SourceControl
{
    public interface IRepositoryManager
    {
        RepositoryType GetRepositoryType();
        void Clean();
    }
}
