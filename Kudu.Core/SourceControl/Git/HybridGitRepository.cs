using System.Collections.Generic;

namespace Kudu.Core.SourceControl.Git {
    /// <summary>
    /// The goal of this repository is to implement a full repository using as much as
    /// libgit2sharp as possible unti it works for all scenarios.
    /// </summary>
    public class HybridGitRepository : IRepository {
        private readonly GitExeRepository _exeRepository;
        private readonly LibGitRepository _libgitRepository;

        public HybridGitRepository(string path) {
            _exeRepository = new GitExeRepository(path);
            _libgitRepository = new LibGitRepository(path);
        }

        public string CurrentId {
            get {
                return _libgitRepository.CurrentId;
            }
        }

        public void Initialize() {
            _exeRepository.Initialize();
        }

        public IEnumerable<Branch> GetBranches() {
            return _libgitRepository.GetBranches();
        }

        public IEnumerable<FileStatus> GetStatus() {
            return _exeRepository.GetStatus();
        }

        public IEnumerable<ChangeSet> GetChanges() {
            // Work around a 2 bugs in libgit2 
            // 1. https://github.com/libgit2/libgit2sharp/issues/51
            // 2. https://github.com/libgit2/libgit2sharp/issues/52
            // return _libgitRepository.GetChanges();
            return _exeRepository.GetChanges();
        }

        public IEnumerable<ChangeSet> GetChanges(int index, int limit) {
            // Work around a 2 bugs in libgit2 
            // 1. https://github.com/libgit2/libgit2sharp/issues/51
            // 2. https://github.com/libgit2/libgit2sharp/issues/52
            // return _libgitRepository.GetChanges(index, limit);
            return _exeRepository.GetChanges(index, limit);
        }

        public ChangeSetDetail GetDetails(string id) {
            return _exeRepository.GetDetails(id);
        }

        public ChangeSetDetail GetWorkingChanges() {
            return _exeRepository.GetWorkingChanges();
        }

        public void AddFile(string path) {
            _exeRepository.AddFile(path);
        }

        public void RevertFile(string path) {
            _exeRepository.RevertFile(path);
        }

        public ChangeSet Commit(string authorName, string message) {
            return _exeRepository.Commit(authorName, message);
        }

        public void Push() {
            _exeRepository.Push();
        }

        public void Update(string id) {
            _exeRepository.Update(id);
        }
    }
}
