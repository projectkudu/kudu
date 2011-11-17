namespace Kudu.Core.SourceControl
{
    public interface IClonableRepository
    {
        void CloneRepository(string source, RepositoryType type);
    }
}
