using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Editor
{
    // TODO: Make testable using IFileSystem
    public class ProjectSystem : IProjectSystem
    {
        private readonly string _root;

        public ProjectSystem(string root)
        {
            _root = root;
        }

        private string GetFullPath(string path)
        {
            string normalizedPath = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
            return Path.Combine(_root, path);
        }

        public string ReadAllText(string path)
        {
            return File.ReadAllText(GetFullPath(path));
        }

        public Project GetProject()
        {
            var solutions = VsHelper.GetSolutions(_root);

            return new Project
            {
                ProjectFiles = (from s in solutions
                                from p in s.Projects
                                where p.IsWap || p.IsWebSite
                                select MakeRelative(p.AbsolutePath)).ToList(),
                Files = GetFiles().ToList(),
                SolutionFiles = solutions.Select(s => MakeRelative(s.Path)).ToList()
            };
        }

        private IEnumerable<string> GetFiles()
        {
            var directory = new DirectoryInfo(_root);
            return GetFiles(directory);
        }

        private IEnumerable<string> GetFiles(DirectoryInfo directory)
        {
            if (directory.Name.StartsWith(".") ||
                directory.Attributes.HasFlag(FileAttributes.Hidden))
            {
                yield break;
            }

            foreach (var file in directory.EnumerateFiles())
            {
                yield return MakeRelative(file.FullName);
            }

            foreach (var subDirectory in directory.EnumerateDirectories())
            {
                var files = GetFiles(subDirectory);

                if (files.Any())
                {
                    yield return MakeRelative(subDirectory.FullName + Path.DirectorySeparatorChar);
                    foreach (var file in files)
                    {
                        yield return file;
                    }
                }
            }
        }

        public void WriteAllText(string path, string text)
        {
            if (path.EndsWith("/", StringComparison.OrdinalIgnoreCase))
            {
                // If the path ends with slash then we're creatng a directory
                // TODO: Consider adding a new method for this.
                FileSystemHelpers.EnsureDirectory(GetFullPath(path));
            }
            else
            {
                File.WriteAllText(GetFullPath(path), text);
            }
        }

        public void Delete(string path)
        {
            File.Delete(GetFullPath(path));
        }

        protected string MakeRelative(string path)
        {
            return path.Substring(_root.Length).TrimStart(Path.DirectorySeparatorChar).Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }
}