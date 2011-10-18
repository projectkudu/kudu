using System;
using System.IO;
using System.Linq;
using Kudu.Core.Infrastructure;
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

            Directory.CreateDirectory(_path);

            switch (type) {
                case RepositoryType.Git:
                    new GitExeRepository(_path).Initialize();
                    break;
                case RepositoryType.Mercurial:
                    new HgRepository(_path).Initialize();
                    break;
                default:
                    throw new InvalidOperationException("Unsupported repository type.");
            }
        }
        
        public void CloneRepository(string source, RepositoryType type) {
            switch (type) {
                case RepositoryType.Git:
                    new GitExeRepository(_path).Clone(source);
                    break;
                case RepositoryType.Mercurial:
                    new HgRepository(_path).Clone(source);
                    break;
            }
        }

        public IRepository GetRepository() {
            RepositoryType type = GetRepositoryType();

            switch (type) {
                case RepositoryType.Git:
                    return new GitExeRepository(_path);
                case RepositoryType.Mercurial:
                    return new HgRepository(_path);
            }

            return null;
        }

        public void Delete() {
            if (Directory.Exists(_path)) {
                FileSystemHelpers.DeleteDirectorySafe(_path);
            }
        }

        public RepositoryType GetRepositoryType() {
            if (!Directory.Exists(_path)) {
                return RepositoryType.None;
            }

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
