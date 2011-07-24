using System.Collections.Generic;
using System.Linq;
using Kudu.Core.Deployment;
using Kudu.Core.SourceControl;
using Kudu.Web.Infrastructure;
using Kudu.Web.Model;
using SignalR.Hubs;

namespace Kudu.Web {
    public class SourceControl : Hub {
        private readonly IRepository _repository;
        private readonly IRepositoryManager _repositoryManager;
        private readonly IDeploymentManager _deploymentManager;

        public SourceControl(IRepository repository,
                             IRepositoryManager repositoryManager,
                             IDeploymentManager deploymentManager) {
            _repository = repository;
            _repositoryManager = repositoryManager;
            _deploymentManager = deploymentManager;
        }

        public ChangeSetDetailViewModel Show(string id) {
            return new ChangeSetDetailViewModel(_repository.GetDetails(id));
        }

        public ChangeSetViewModel Commit(string message) {
            var changeSet = _repository.Commit("Test <foo@test.com>", message);
            if (changeSet != null) {
                // Deploy after comitting
                _deploymentManager.Deploy(changeSet.Id);
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
                                 .ToDictionary(p => p.Key, p => p.Select(b => b.Name))
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

        public void Revert(string path) {
            _repository.RevertFile(path);
        }

        public IEnumerable<FileStatus> GetStatus() {
            return _repository.GetStatus();
        }

        public void Deploy(string id) {
            _repository.Update(id);
            _deploymentManager.Deploy(id);
        }
    }
}
