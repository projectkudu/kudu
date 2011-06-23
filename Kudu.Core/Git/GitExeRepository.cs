using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kudu.Core.Infrastructure;
using System.Text;

namespace Kudu.Core.Git {
    /// <summary>
    /// Implementation of a git repository over git.exe
    /// </summary>
    public class GitExeRepository : IRepository {
        private readonly Executable _gitExe;
        private const char CommitSeparator = '`';
        private const char CommitInfoSeparator = '|';

        public GitExeRepository(string path)
            : this(ResolveGitPath(), path) {

        }

        public GitExeRepository(string pathToGitExe, string path) {
            _gitExe = new Executable(pathToGitExe, path);
        }

        public string CurrentId {
            get {
                var head = Head;
                if (head != null) {
                    return head.Id;
                }
                return null;
            }
        }

        private ChangeSet Head {
            get {
                return Log(1, all: false).SingleOrDefault();
            }
        }

        public void Initialize() {
            _gitExe.Execute("init");
            _gitExe.Execute("config core.autocrlf true");
        }

        public IEnumerable<FileStatus> GetStatus() {
            return ParseStatus(_gitExe.Execute("status --porcelain"));
        }

        public IEnumerable<ChangeSet> GetChanges() {
            return Log();
        }

        public void AddFile(string path) {
            _gitExe.Execute("add {0}", path);
        }

        public void RemoveFile(string path) {
            _gitExe.Execute("rm {0} --cached", path);
        }

        public ChangeSet Commit(string authorName, string message) {
            // Add all unstaged files
            _gitExe.Execute("add .");
            _gitExe.Execute("commit -m\"{0}\" --author=\"{1}\"", message, authorName);

            return Head;
        }

        public void Update(string id) {
            _gitExe.Execute("checkout {0} --force", id);
        }

        public ChangeSetDetail GetDetails(string id) {
            return ParseShow(_gitExe.Execute("show {0} --patch-with-stat --format=\"%H{1}%aN{1}%B{1}%ci{2}\"", id, CommitInfoSeparator, CommitSeparator));
        }

        private bool IsEmpty() {
            // REVIEW: Is this reliable
            return String.IsNullOrWhiteSpace(_gitExe.Execute("branch"));
        }

        private static string ResolveGitPath() {
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string path = Path.Combine(programFiles, "Git", "bin", "git.exe");

            if (!File.Exists(path)) {
                throw new InvalidOperationException("Unable to locate git.exe");
            }

            return path;
        }

        private IEnumerable<ChangeSet> Log(int? limit = null, bool all = true) {
            if (IsEmpty()) {
                return Enumerable.Empty<ChangeSet>();
            }
            string command = "log --format=\"%H{0}%aN{0}%B{0}%ci{1}\"";
            if (limit != null) {
                command += " -" + limit;
            }

            if (all) {
                command += " --all";
            }

            return ParseCommits(_gitExe.Execute(command, CommitInfoSeparator, CommitSeparator));
        }

        private IEnumerable<ChangeSet> ParseCommits(string raw) {
            foreach (var line in raw.Split(new[] { CommitSeparator }, StringSplitOptions.RemoveEmptyEntries)) {
                if (String.IsNullOrWhiteSpace(line)) {
                    continue;
                }
                string commit = line.Trim();
                yield return ParseCommit(new BetterStringReader(commit));
            }
        }

        private ChangeSetDetail ParseShow(string raw) {
            var reader = new BetterStringReader(raw);

            // Parse the changeset
            ChangeSet changeSet = ParseCommit(reader);

            // Skip the --- separator
            reader.ReadUntil("---");
            reader.Skip(3);

            var detail = new ChangeSetDetail(changeSet);

            ParseSummary(reader, detail);

            foreach (var diff in ParseDiff(reader)) {
                detail.Diffs.Add(diff);
            }

            return detail;
        }

        private void ParseSummary(BetterStringReader reader, ChangeSetDetail detail) {
            string summary = reader.ReadUntil("diff").Trim();
            var summaryReader = summary.AsReader();

            do {
                string line = summaryReader.ReadLine();
                if (line.Contains("|")) {
                    string[] parts = line.Split('|');
                    detail.FileStats[parts[0].Trim()] = ParseStats(parts[1].Trim());
                }
                else {
                    // n files changed, n insertions(+), n deletions(-)
                    var subReader = line.AsReader();
                    subReader.SkipWhitespace();
                    detail.FilesChanged = subReader.ReadInt().Value;
                    subReader.ReadUntil(',');
                    subReader.Skip(1);
                    subReader.SkipWhitespace();
                    detail.Insertions = subReader.ReadInt().Value;
                    subReader.ReadUntil(',');
                    subReader.Skip(1);
                    subReader.SkipWhitespace();
                    detail.Deletions = subReader.ReadInt().Value;
                    break;
                }

            } while (true);
        }

        private FileStats ParseStats(string raw) {
            return new FileStats {
                Deletions = raw.Count(c => c == '-'),
                Insertions = raw.Count(c => c == '+'),
            };
        }

        private IEnumerable<FileDiff> ParseDiff(BetterStringReader reader) {
            do {
                string diffHeader = reader.ReadLine();
                if (String.IsNullOrWhiteSpace(diffHeader)) {
                    break;
                }

                string diffChunk = diffHeader + reader.ReadUntil("diff");
                yield return ParseDiffChunk(new BetterStringReader(diffChunk));

            } while (true);
        }

        private FileDiff ParseDiffChunk(BetterStringReader reader) {
            // Extract the file name from the diff header
            var headerReader = reader.ReadLine().AsReader();
            headerReader.ReadUntil("a/");
            headerReader.Skip(2);
            string fileName = headerReader.ReadUntilWhitespace();
            string result;
            var diff = new FileDiff(fileName);

            // Parse the diff
            if (reader.TryReadUntil("@@", out result)) {
                string line = reader.ReadLine();
                while ((line = reader.ReadLine()) != null) {
                    if (line.StartsWith("+")) {
                        diff.Lines.Add(new LineDiff(ChangeType.Added, line.Substring(1)));
                    }
                    else if (line.StartsWith("-")) {
                        diff.Lines.Add(new LineDiff(ChangeType.Deleted, line.Substring(1)));
                    }
                    else {
                        diff.Lines.Add(new LineDiff(ChangeType.None, line));
                    }
                }
            }
            return diff;
        }

        private ChangeSet ParseCommit(BetterStringReader reader) {
            string id = reader.ReadUntil(CommitInfoSeparator);
            reader.Skip();
            string author = reader.ReadUntil(CommitInfoSeparator);
            reader.Skip();
            string message = reader.ReadUntil(CommitInfoSeparator);
            reader.Skip();
            string date = reader.ReadUntil(CommitSeparator);
            return new ChangeSet(id, author, message, DateTimeOffset.Parse(date));
        }

        private IEnumerable<FileStatus> ParseStatus(string raw) {
            foreach (var line in raw.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)) {
                // Status Path
                string status = line.Substring(0, 2).Trim();
                string path = line.Substring(2).Trim();
                yield return new FileStatus(path, Convert(status));
            }
        }

        private ChangeType Convert(string status) {
            switch (status) {
                case "A":
                case "AM":
                    return ChangeType.Added;
                case "M":
                    return ChangeType.Modified;
                case "D":
                    return ChangeType.Deleted;
                case "R":
                    return ChangeType.Renamed;
                case "??":
                    return ChangeType.Untracked;
                default:
                    break;
            }

            throw new InvalidOperationException("Unsupported status " + status);
        }
    }
}
