using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kudu.Core.SourceControl;
using Kudu.Core.SourceControl.Git;
using Kudu.Core.SourceControl.Hg;

namespace Kudu.Services.Web {
    public class ServiceRepository : IRepository {
        private readonly IRepository _repository;
        public ServiceRepository(ILocationProvider locationProvider) {
            _repository = GetRepository(locationProvider);
        }

        public string CurrentId {
            get {
                return _repository.CurrentId;
            }
        }

        public void Initialize() {
            _repository.Initialize();
        }

        public IEnumerable<Branch> GetBranches() {
            return _repository.GetBranches();
        }

        public IEnumerable<FileStatus> GetStatus() {
            return _repository.GetStatus();
        }

        public IEnumerable<ChangeSet> GetChanges() {
            return _repository.GetChanges();
        }

        public IEnumerable<ChangeSet> GetChanges(int index, int limit) {
            return _repository.GetChanges(index, limit);
        }

        public ChangeSetDetail GetDetails(string id) {
            return _repository.GetDetails(id);
        }

        public ChangeSetDetail GetWorkingChanges() {
            return _repository.GetWorkingChanges();
        }

        public void AddFile(string path) {
            _repository.AddFile(path);
        }

        public void RemoveFile(string path) {
            _repository.RemoveFile(path);
        }

        public ChangeSet Commit(string authorName, string message) {
            return _repository.Commit(authorName, message);
        }

        public void Update(string id) {
            _repository.Update(id);
        }

        private static IRepository GetRepository(ILocationProvider locationProvider) {
            string path = locationProvider.RepositoryRoot;

            if (!Directory.Exists(path)) {
                Directory.CreateDirectory(path);
            }

            if (Directory.EnumerateDirectories(path, ".hg").Any()) {
                return new HgRepository(path);
            }

            return new HybridGitRepository(path);
        }
    }
}