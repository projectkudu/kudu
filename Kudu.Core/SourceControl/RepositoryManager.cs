using System;
using System.IO;
using System.Linq;
using Kudu.Core.SourceControl.Git;
using Kudu.Core.SourceControl.Hg;

namespace Kudu.Core.SourceControl {
    public class RepositoryManager : IRepositoryManager {
        private readonly string _path;

        public RepositoryManager(string path) {
            _path = path;
        }

        
        public void CreateRepository(RepositoryType type) {
            RepositoryType currentType = GetRepositoryType();
            
            if (currentType != RepositoryType.None) {
                throw new InvalidOperationException("Repository already exists. Delete it before creating a new one.");
            }

            switch (type) {
                case RepositoryType.Git:
                    new HybridGitRepository(_path).Initialize();
                    break;
                case RepositoryType.Mercurial:
                    new HgRepository(_path).Initialize();
                    break;
                default:
                    throw new InvalidOperationException("Unsupported repository type.");
            }
        }

        public IRepository GetRepository() {
            RepositoryType type = GetRepositoryType();

            switch (type) {
                case RepositoryType.Git:
                    return new HybridGitRepository(_path);
                case RepositoryType.Mercurial:
                    return new HgRepository(_path);
            }

            return null;
        }

        public void Delete() {
            throw new NotImplementedException();
        }

        public RepositoryType GetRepositoryType() {
            if (Directory.EnumerateDirectories(_path, ".hg").Any()) {
                return RepositoryType.Mercurial;
            }
            else if (Directory.EnumerateDirectories(_path, ".git").Any()) {
                return RepositoryType.Git;
            }
            return RepositoryType.None;
        }
    }
}
