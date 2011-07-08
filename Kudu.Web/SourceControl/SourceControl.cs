using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Kudu.Core;
using Kudu.Core.SourceControl.Git;
using Kudu.Core.SourceControl.Hg;
using Kudu.Core.SourceControl;
using SignalR.Hubs;

namespace Kudu.Web {
    public class SourceControl : Hub {
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

        public IDictionary<string, IEnumerable<string>> GetBranches() {
            var repository = GetRepository();

            return repository.GetBranches()
                             .ToLookup(b => b.Id)
                             .ToDictionary(p => p.Key, p => p.Select(b => b.Name));
        }

        public IEnumerable<ChangeSetViewModel> GetChanges(int index, int pageSize) {
            var repository = GetRepository();
            string id = repository.CurrentId;
            Caller.id = id;

            return from c in repository.GetChanges(index, pageSize)
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
        }

        private IRepository GetRepository() {
            string path = Caller.path;

            if (String.IsNullOrEmpty(path)) {
                throw new InvalidOperationException("No repository path!");
            }

            var uri = new Uri(path);          

            if (!uri.IsFile && !uri.IsUnc) {
                return new RemoteRepository(path);
            }

            if (Directory.EnumerateDirectories(path, ".hg").Any()) {
                return new HgRepository(path);
            }
            return new HybridRepository(path);
        }

        public class ChangeSetDetailViewModel {
            public ChangeSetDetailViewModel(ChangeSetDetail detail) {
                if (detail.ChangeSet != null) {
                    ChangeSet = new ChangeSetViewModel(detail.ChangeSet);
                }
                Deletions = detail.Deletions;
                FilesChanged = detail.FilesChanged;
                Insertions = detail.Insertions;
                Files = detail.Files;
            }

            public ChangeSetViewModel ChangeSet { get; set; }
            public int Deletions { get; set; }
            public int FilesChanged { get; set; }
            public int Insertions { get; set; }
            public IDictionary<string, Kudu.Core.SourceControl.FileInfo> Files { get; set; }
        }

        public class ChangeSetViewModel {
            public string Id { get; set; }
            public string ShortId { get; set; }
            public string AuthorName { get; set; }
            public string EmailHash { get; set; }
            public string Date { get; set; }
            public string Message { get; set; }
            public string Summary { get; set; }
            public bool Active { get; set; }

            public ChangeSetViewModel(ChangeSet changeSet) {
                Id = changeSet.Id;
                ShortId = changeSet.Id.Substring(0, 12);
                AuthorName = changeSet.AuthorName;
                EmailHash = String.IsNullOrEmpty(changeSet.AuthorEmail) ? null : Hash(changeSet.AuthorEmail);
                Date = changeSet.Timestamp.ToString("u");
                Message = Process(changeSet.Message);
                // Show first line only
                var reader = new StringReader(changeSet.Message);
                Summary = Process(Trim(reader.ReadLine(), 300));
            }

            private string Trim(string value, int max) {
                if (String.IsNullOrEmpty(value)) {
                    return value;
                }

                if (value.Length > max) {
                    return value.Substring(0, max) + "...";
                }
                return value;
            }

            private string Process(string value) {
                if (String.IsNullOrEmpty(value)) {
                    return value;
                }
                return value.Trim().Replace("\n", "<br/>");
            }

            private string Hash(string value) {
                return String.Join(String.Empty, MD5.Create()
                         .ComputeHash(Encoding.Default.GetBytes(value))
                         .Select(b => b.ToString("x2")));
            }
        }
    }
}
