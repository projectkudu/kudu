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
        private readonly IUserInformation _userInformation;

        public SourceControl(IRepository repository,
                             IUserInformation userInformation) {
            _repository = repository;
            _userInformation = userInformation;
        }

        public ChangeSetDetailViewModel Show(string id) {
            return new ChangeSetDetailViewModel(_repository.GetDetails(id));
        }

        public ChangeSetViewModel Commit(string message) {
            var changeSet = _repository.Commit(_userInformation.UserName, message);
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
            return new RepositoryViewModel {
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

        public void Revert(string path) {
            _repository.RevertFile(path);
        }

        public IEnumerable<FileStatus> GetStatus() {
            return _repository.GetStatus();
        }
        
        public void Push() {
            _repository.Push();
        }
    }
}
