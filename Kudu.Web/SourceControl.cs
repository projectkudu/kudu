using System;
using System.Collections.Generic;
using System.Linq;
using Kudu.Core;
using Kudu.Core.Git;
using ServerSync;

namespace Kudu.Web {
    public class SourceControl : Hub {
        private const string RepositoryPath = @"C:\dev\github\NGitHub";

        public IEnumerable<ChangeSetViewModel> GetChanges() {
            var repository = new GitExeRepository(RepositoryPath);
            Caller.id = repository.CurrentId;
            return (from c in repository.GetChanges()
                    select new ChangeSetViewModel(c) {
                        Active = c.Id == repository.CurrentId
                    }).Take(10);
        }

        public IEnumerable<FileStatus> GetStatus() {
            var repository = new GitExeRepository(RepositoryPath);
            return repository.GetStatus();
        }

        public void Update(string changeSetId) {
            var repository = new GitExeRepository(RepositoryPath);

            repository.Update(changeSetId);

            Caller.id = changeSetId;
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