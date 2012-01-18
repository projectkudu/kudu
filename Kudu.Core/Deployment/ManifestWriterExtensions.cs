using System.Collections.Generic;
using System.IO;

namespace Kudu.Core.Deployment
{
    public static class ManifestWriterExtensions
    {
        public static void AddFiles(this IDeploymentManifestWriter writer, string directory)
        {
            var files = GetFiles(directory);
            writer.AddPaths(files);
        }

        private static IEnumerable<string> GetFiles(string directory)
        {
            foreach (var file in Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories))
            {
                yield return file.Substring(directory.Length).TrimStart('\\');
            }
        }
    }
}
