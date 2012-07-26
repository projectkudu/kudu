using System.IO;

namespace Kudu.TestHarness
{
    public static class TestRepositoryExtensions
    {
        public static void WriteFile(this TestRepository repository, string path, string contents)
        {
            File.WriteAllText(Path.Combine(repository.PhysicalPath, path), contents);
        }

        public static void AppendFile(this TestRepository repository, string path, string contents)
        {
            File.AppendAllText(Path.Combine(repository.PhysicalPath, path), contents);
        }

        public static void Replace(this TestRepository repository, string path, string oldValue, string newValue)
        {
            repository.WriteFile(path, repository.ReadFile(path).Replace(oldValue, newValue));
        }

        public static string ReadFile(this TestRepository repository, string path)
        {
            return File.ReadAllText(Path.Combine(repository.PhysicalPath, path));
        }

        public static bool FileExists(this TestRepository repository, string path)
        {
            return File.Exists(Path.Combine(repository.PhysicalPath, path));
        }

        public static void DeleteFile(this TestRepository repository, string path)
        {
            File.Delete(Path.Combine(repository.PhysicalPath, path));
        }
    }
}
