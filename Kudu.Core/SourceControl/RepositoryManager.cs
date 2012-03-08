using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Kudu.Core.Infrastructure;
using Kudu.Core.SourceControl.Git;

namespace Kudu.Core.SourceControl
{
    public class RepositoryManager : IRepositoryManager
    {
        private readonly string _path;

        public RepositoryManager(string path)
        {
            _path = path;
        }

        public void Clean()
        {
            // Assume git for now since that's all we support
            var repo = new GitExeRepository(_path);

            repo.Clean();
        }

        public RepositoryType GetRepositoryType()
        {
            return GetRepositoryType(_path);
        }

        public static RepositoryType GetRepositoryType(string path)
        {
            if (!Directory.Exists(path))
            {
                return RepositoryType.None;
            }

            if (Directory.EnumerateDirectories(path, ".hg").Any())
            {
                return RepositoryType.Mercurial;
            }
            else if (Directory.EnumerateDirectories(path, ".git").Any())
            {
                return RepositoryType.Git;
            }

            return RepositoryType.None;
        }
    }
}
