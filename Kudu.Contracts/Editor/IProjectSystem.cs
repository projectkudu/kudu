
namespace Kudu.Core.Editor
{
    public interface IProjectSystem
    {
        string ReadAllText(string path);
        Project GetProject();
        void WriteAllText(string path, string content);
        void Delete(string path);
    }
}
