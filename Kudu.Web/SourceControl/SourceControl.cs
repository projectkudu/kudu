using System;
using System.Collections.Generic;
using System.Linq;
using Kudu.Core;
using Kudu.Core.Git;
using ServerSync;

namespace Kudu.Web {
    public class SourceControl : Hub {
        public void Connect(string path) {
            Caller.repository = path;
        }

        public ChangeSetDetail Show(string id) {
            var repository = GetRepository();
            return repository.GetDetails(id);
        }

        public IEnumerable<ChangeSetViewModel> GetChanges() {
            var repository = GetRepository();
            Caller.id = repository.CurrentId;
            return from c in repository.GetChanges()
                   select new ChangeSetViewModel(c) {
                       Active = c.Id == repository.CurrentId
                   };
        }

        public IEnumerable<FileStatus> GetStatus() {
            return GetRepository().GetStatus();
        }

        public void Update(string id) {
            var repository = GetRepository();

            repository.Update(id);

            Caller.id = id;
        }

        private IRepository GetRepository() {
            return new GitExeRepository(Caller.repository);
        }

        public class ChangeSetViewModel {
            public string Id { get; set; }
            public string ShortId { get; set; }
            public string AuthorName { get; set; }
            public string Date { get; set; }
            public string Message { get; set; }
            public bool Active { get; set; }

            public ChangeSetViewModel(ChangeSet changeSet) {
                Id = changeSet.Id;
                ShortId = changeSet.Id.Substring(0, 12);
                AuthorName = changeSet.AuthorName;
                Date = changeSet.Timestamp.ToLocalTime().ToString();
                Message = changeSet.Message.Trim().Replace(Environment.NewLine, "<br/>");
            }
        }
    }
}