using System.IO;

namespace Kudu.FunctionalTests.Infrastructure
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
    }
}
