using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Kudu.Core;
using Kudu.Core.Git;
using Kudu.Core.Hg;
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

        public BufferedResult<ChangeSetViewModel> GetChanges(int index) {
            const int pageSize = 25;
            var repository = GetRepository();
            string id = repository.CurrentId;
            Caller.id = id;

            var items = (from c in repository.GetChanges(index * pageSize, pageSize)
                         select new ChangeSetViewModel(c) {
                             Active = c.Id == id
                         })
                         .ToList();

            return new BufferedResult<ChangeSetViewModel> {
                Current = index + 1,
                Items = items,
                More = items.Count >= pageSize
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
            string path = Caller.repository;
            if (Directory.EnumerateDirectories(path, ".hg").Any()) {
                return new HgRepository(path);
            }
            return new GitExeRepository(path);
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
            public IDictionary<string, Kudu.Core.FileInfo> Files { get; set; }
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
                Summary = Process(Trim(changeSet.Message, 300));
            }

            private string Trim(string value, int max) {
                if (value.Length > max) {
                    return value.Substring(0, max) + "...";
                }
                return value;
            }

            private string Process(string value) {
                return value.Trim().Replace("\n", "<br/>");
            }

            private string Hash(string value) {
                return String.Join(String.Empty, MD5.Create()
                         .ComputeHash(Encoding.Default.GetBytes(value))
                         .Select(b => b.ToString("x2")));
            }
        }

        public class BufferedResult<T> {
            public bool More { get; set; }
            public int Current { get; set; }
            public IEnumerable<T> Items { get; set; }
        }
    }
}
