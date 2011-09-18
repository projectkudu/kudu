using System.Collections.Generic;
using System.Linq;
using Kudu.Client.Infrastructure;
using Kudu.Client.Model;
using Kudu.Core.Deployment;
using Kudu.Core.SourceControl;
using SignalR.Hubs;

namespace Kudu.Client {
    public class SourceControl : Hub {
        private readonly IRepository _repository;
        private readonly IRepositoryManager _repositoryManager;
        private readonly IDeploymentManager _deploymentManager;
        private readonly IUserInformation _userInformation;

        public SourceControl(IRepository repository,
                             IRepositoryManager repositoryManager,
                             IDeploymentManager deploymentManager,
                             IUserInformation userInformation) {
            _repository = repository;
            _repositoryManager = repositoryManager;
            _deploymentManager = deploymentManager;
            _userInformation = userInformation;
        }

        public ChangeSetDetailViewModel Show(string id) {
            return new ChangeSetDetailViewModel(_repository.GetDetails(id));
        }

        public ChangeSetViewModel Commit(string message) {
            var changeSet = _repository.Commit(_userInformation.UserName, message);
            if (changeSet != null) {
                // Deploy after comitting
                _deploymentManager.Build(changeSet.Id);
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
            Caller.id = _deploymentManager.ActiveDeploymentId;

            var type = _repositoryManager.GetRepositoryType();
            return new RepositoryViewModel {
                Deployments = _deploymentManager.GetResults()
                                                .ToDictionary(d => d.Id,
                                                              d => new DeployResultViewModel(d)),
                Branches = _repository.GetBranches()
                                 .ToLookup(b => b.Id)
                                 .ToDictionary(p => p.Key, p => p.Select(b => b.Name))
            };
        }

        public IEnumerable<ChangeSetViewModel> GetChanges(int index, int pageSize) {
            string id = Caller.id;

            return from c in _repository.GetChanges(index, pageSize)
                   select new ChangeSetViewModel(c) {
                       Active = c.Id == id
                   };
        }

        public IEnumerable<LogEntryViewModel> GetDeployLog(string id) {
            return from entry in _deploymentManager.GetLogEntries(id)
                   select new LogEntryViewModel(entry);
        }

        public void Revert(string path) {
            _repository.RevertFile(path);
        }

        public IEnumerable<FileStatus> GetStatus() {
            return _repository.GetStatus();
        }

        public void Deploy(string id) {
            _deploymentManager.Deploy(id);
        }
    }
}
