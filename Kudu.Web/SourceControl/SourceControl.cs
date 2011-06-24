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

        public ChangeSetDetailViewModel Show(string id) {
            var repository = GetRepository();
            return new ChangeSetDetailViewModel(repository.GetDetails(id));
        }

        public ChangeSetViewModel Commit(string message) {
            var repository = GetRepository();
            var changeSet = repository.Commit("Test <foo@test.com>", message);
            if (changeSet != null) {
                return new ChangeSetViewModel(changeSet);
            }
            return null;
        }

        public ChangeSetDetailViewModel GetWorking() {
            var repository = GetRepository();
            ChangeSetDetail workingChanges = repository.GetWorkingChanges();
            if (workingChanges != null) {
                return new ChangeSetDetailViewModel(workingChanges);
            }
            return null;
        }

        public IEnumerable<ChangeSetViewModel> GetChanges() {
            var repository = GetRepository();
            string id = repository.CurrentId;
            Caller.id = id;
            return from c in repository.GetChanges()
                   select new ChangeSetViewModel(c) {
                       Active = c.Id == id
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

        public class ChangeSetDetailViewModel {
            public ChangeSetDetailViewModel(ChangeSetDetail detail) {
                if (detail.ChangeSet != null) {
                    ChangeSet = new ChangeSetViewModel(detail.ChangeSet);
                }
                Deletions = detail.Deletions;
                FilesChanged = detail.FilesChanged;
                Insertions = detail.Insertions;
                FileStats = detail.FileStats;
                Diffs = detail.Diffs;
            }

            public ChangeSetViewModel ChangeSet { get; set; }
            public int Deletions { get; set; }
            public int FilesChanged { get; set; }
            public int Insertions { get; set; }
            public IDictionary<string, FileStats> FileStats { get; set; }
            public IList<FileDiff> Diffs { get; set; }
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
                Date = changeSet.Timestamp.ToString("u");
                Message = changeSet.Message.Trim().Replace("\n", "<br/>");
            }
        }
    }
}
