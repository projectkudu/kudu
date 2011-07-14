using System.Collections.Generic;
using System.Linq;
using Kudu.Core.SourceControl;
using Kudu.Web.Model;
using SignalR.Hubs;

namespace Kudu.Web {
    public class SourceControl : Hub {
        private readonly IRepository _repository;
        private readonly IRepositoryManager _repositoryManager;

        public SourceControl(IRepository repository,
                             IRepositoryManager repositoryManager) {
            _repository = repository;
            _repositoryManager = repositoryManager;
        }

        public ChangeSetDetailViewModel Show(string id) {
            return new ChangeSetDetailViewModel(_repository.GetDetails(id));
        }

        public ChangeSetViewModel Commit(string message) {
            var changeSet = _repository.Commit("Test <foo@test.com>", message);
            if (changeSet != null) {
                return new ChangeSetViewModel(changeSet);
            }
            return null;
        }

        public ChangeSetDetailViewModel GetWorking() {
            ChangeSetDetail workingChanges = _repository.GetWorkingChanges();
            if (workingChanges != null) {
                return new ChangeSetDetailViewModel(workingChanges);
            }
            return null;
        }

        public RepositoryViewModel GetRepositoryInfo() {
            var type = _repositoryManager.GetRepositoryType();
            return new RepositoryViewModel {
                Branches = _repository.GetBranches()
                                 .ToLookup(b => b.Id)
                                 .ToDictionary(p => p.Key, p => p.Select(b => b.Name)),
                RepositoryType = type.ToString(),
                CloneUrl = type == RepositoryType.Git ? "http://localhost:52590/SampleRepo.git" : null
            };
        }

        public IEnumerable<ChangeSetViewModel> GetChanges(int index, int pageSize) {
            string id = _repository.CurrentId;
            Caller.id = id;

            return from c in _repository.GetChanges(index, pageSize)
                   select new ChangeSetViewModel(c) {
                       Active = c.Id == id
                   };
        }

        public IEnumerable<FileStatus> GetStatus() {
            return _repository.GetStatus();
        }

        public void Update(string id) {
            _repository.Update(id);
        }
    }
}
