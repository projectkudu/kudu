using System;
using System.Collections.Generic;
using System.Linq;
using Kudu.Core.Git;
using Kudu.Core.Infrastructure;
using Mercurial;

namespace Kudu.Core.Hg {
    public class HgRepository : IRepository {
        private readonly Repository _repository;
        public HgRepository(string path) {
            _repository = new Repository(path);
        }

        public string CurrentId {
            get {
                string id = _repository.Identify();
                // Get the full hash
                return _repository.Log(id).Single().Hash;
            }
        }

        public void Initialize() {
            _repository.Init();
        }

        public IEnumerable<FileStatus> GetStatus() {
            throw new NotImplementedException();
        }

        public IEnumerable<ChangeSet> GetChanges() {
            return from c in _repository.Log()
                   select CreateChangeSet(c);
        }

        public ChangeSetDetail GetDetails(string id) {
            var changeSet = GetChangeSet(id);

            var detail = new ChangeSetDetail(changeSet);

            return PopulateDetails(id, detail);
        }

        public ChangeSetDetail GetWorkingChanges() {
            return PopulateDetails(null, new ChangeSetDetail());
        }

        public void AddFile(string path) {
            _repository.Add(path);
        }

        public void RemoveFile(string path) {
            _repository.Remove(path);
        }

        public ChangeSet Commit(string authorName, string message) {
            _repository.AddRemove();

            var id = _repository.Commit(new CommitCommand {
                OverrideAuthor = authorName,
                Message = message
            });

            return GetChangeSet(id);
        }

        public void Update(string id) {
            _repository.Update(id);
        }

        private ChangeSetDetail PopulateDetails(string id, ChangeSetDetail detail) {
            var summaryCommand = new DiffCommand {
                SummaryOnly = true
            };

            if (!String.IsNullOrEmpty(id)) {
                summaryCommand.ChangeIntroducedByRevision = id;
            }

            IStringReader summaryReader = _repository.Diff(summaryCommand).AsReader();

            ParseSummary(summaryReader, detail);

            var diffCommand = new DiffCommand {
                UseGitDiffFormat = true,
            };

            if (!String.IsNullOrEmpty(id)) {
                diffCommand.ChangeIntroducedByRevision = id;
            }

            var diffReader = _repository.Diff(diffCommand).AsReader();

            foreach (var diff in GitExeRepository.ParseDiff(diffReader)) {
                detail.Diffs.Add(diff);
            }

            return detail;
        }

        private void ParseSummary(IStringReader reader, ChangeSetDetail detail) {
            while (!reader.Done) {
                string line = reader.ReadLine();
                if (line.Contains("|")) {
                    string[] parts = line.Split('|');
                    detail.FileStats[parts[0].Trim()] = new FileStats {
                    };
                }
                else {
                    // n files changed, n insertions(+), n deletions(-)
                    ParserHelpers.ParseSummaryFooter(line, detail);
                }
            }
        }

        private ChangeSet GetChangeSet(string id) {
            var log = _repository.Log(id);
            return CreateChangeSet(log.SingleOrDefault());
        }

        private ChangeSet CreateChangeSet(Changeset changeSet) {
            return new ChangeSet(changeSet.Hash, changeSet.AuthorName, changeSet.AuthorEmailAddress, changeSet.CommitMessage, new DateTimeOffset(changeSet.Timestamp));
        }
    }
}
