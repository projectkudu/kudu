using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Kudu.Core.Git;
using Kudu.Core.Infrastructure;
using Mercurial;

namespace Kudu.Core.Hg {
    public class HgRepository : IRepository {
        private readonly Repository _repository;
        private readonly Executable _hgExe;

        public HgRepository(string path) {
            _repository = new Repository(path);
            _hgExe = new Executable(Client.ClientPath, path);
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
            return from fileStatus in _repository.Status()
                   let path = fileStatus.Path.Replace('\\', '/')
                   select new FileStatus(path, Convert(fileStatus.State));
        }

        public IEnumerable<ChangeSet> GetChanges() {
            const int bufferSize = 10;

            int index = 0;
            bool some = false;

            do {
                some = false;
                foreach (var changeSet in GetChanges(index, bufferSize)) {
                    some = true;
                    yield return changeSet;
                }

                index += bufferSize;
            } while (some);
        }

        public IEnumerable<ChangeSet> GetChanges(int index, int limit) {
            int max = _repository.Tip().RevisionNumber;

            if (index > max) {
                return Enumerable.Empty<ChangeSet>();
            }

            int from = max - index;
            int to = Math.Max(0, from - limit);
            var spec = RevSpec.Range(from, to);
            return _repository.Log(spec).Select(CreateChangeSet);
        }

        public ChangeSetDetail GetDetails(string id) {
            var changeSet = GetChangeSet(id);

            var detail = new ChangeSetDetail(changeSet);

            return PopulateDetails(id, detail);
        }

        public ChangeSetDetail GetWorkingChanges() {
            var status = GetStatus().ToList();
            if (!status.Any()) {
                return null;
            }

            _repository.AddRemove();
            var detail = PopulateDetails(null, new ChangeSetDetail());

            foreach (var fileStatus in status) {
                FileInfo info;
                if (detail.Files.TryGetValue(fileStatus.Path, out info)) {
                    info.Status = fileStatus.Status;
                }
            }

            return detail;
        }

        public void AddFile(string path) {
            _repository.Add(path);
        }

        public void RemoveFile(string path) {
            _repository.Remove(path);
        }

        public ChangeSet Commit(string authorName, string message) {
            _repository.AddRemove();

            try {
                var id = _repository.Commit(new CommitCommand {
                    OverrideAuthor = authorName,
                    Message = message
                });

                return GetChangeSet(id);
            }
            catch {
                return null;
            }
        }

        public void Update(string id) {
            _repository.Update(id);
        }

        public IEnumerable<Branch> GetBranches() {
            // Need to work around a bug in Mercurial.net where it fails to parse the output of 
            // the branches command (http://mercurialnet.codeplex.com/workitem/14)
            var branchReader = _hgExe.Execute("branches").AsReader();

            while (!branchReader.Done) {                
                // name WS revision:hash
                string line = branchReader.ReadLine();
                var match = Regex.Match(line, @"(?<rev>\d+)\:");
                if (match.Success) {
                    string name = line.Substring(0, match.Index).Trim();
                    int revision = Int32.Parse(match.Groups["rev"].Value);
                    string id = GetChangeSet(revision).Id;
                    yield return new Branch(id, name);
                }
            }
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

            GitExeRepository.ParseDiffAndPopulate(diffReader, detail);

            return detail;
        }

        private void ParseSummary(IStringReader reader, ChangeSetDetail detail) {
            while (!reader.Done) {
                string line = reader.ReadLine();
                if (line.Contains("|")) {
                    string[] parts = line.Split('|');
                    string path = parts[0].Trim();

                    // TODO: Figure out a way to get this information
                    detail.Files[path] = new FileInfo();
                }
                else {
                    // n files changed, n insertions(+), n deletions(-)
                    ParserHelpers.ParseSummaryFooter(line, detail);
                }
            }
        }

        private ChangeSet GetChangeSet(RevSpec id) {
            var log = _repository.Log(id);
            return CreateChangeSet(log.SingleOrDefault());
        }

        private ChangeSet CreateChangeSet(Changeset changeSet) {
            return new ChangeSet(changeSet.Hash, changeSet.AuthorName, changeSet.AuthorEmailAddress, changeSet.CommitMessage, new DateTimeOffset(changeSet.Timestamp));
        }

        private ChangeType Convert(FileState state) {
            switch (state) {
                case FileState.Added:
                    return ChangeType.Added;
                case FileState.Clean:
                    break;
                case FileState.Ignored:
                    break;
                case FileState.Missing:
                    break;
                case FileState.Modified:
                    return ChangeType.Modified;
                case FileState.Removed:
                    return ChangeType.Deleted;
                case FileState.Unknown:
                    return ChangeType.Untracked;
                default:
                    break;
            }

            throw new InvalidOperationException("Unsupported status " + state);
        }
    }
}
