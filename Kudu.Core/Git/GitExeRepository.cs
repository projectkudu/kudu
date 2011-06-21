using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Git {
    /// <summary>
    /// Implementation of a git repository over git.exe
    /// </summary>
    public class GitExeRepository : IRepository {
        private readonly Executable _gitExe;
        private const char CommitSeparator = '~';
        private const char CommitInfoSeparator = '|';

        public GitExeRepository(string path)
            : this(ResolveGitPath(), path) {

        }

        public GitExeRepository(string pathToGitExe, string path) {
            _gitExe = new Executable(pathToGitExe, path);
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

            return Log(1).Single();
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

        private IEnumerable<ChangeSet> Log(int? limit = null) {
            if (IsEmpty()) {
                return Enumerable.Empty<ChangeSet>();
            }
            string command = "log --format=\"%H{0}%aN{0}%B{0}%ci{1}\"";
            if (limit != null) {
                command += " -" + limit;
            }
            return ParseCommits(_gitExe.Execute(command, CommitInfoSeparator, CommitSeparator));
        }

        private IEnumerable<ChangeSet> ParseCommits(string raw) {
            foreach (var line in raw.Split(new[] { CommitSeparator }, StringSplitOptions.RemoveEmptyEntries)) {
                if (String.IsNullOrWhiteSpace(line)) {
                    continue;
                }
                string[] parts = line.Trim().Split(CommitInfoSeparator);
                string id = parts[0];
                string author = parts[1];
                string message = parts[2];
                string date = parts[3];
                yield return new ChangeSet(id, author, message, DateTimeOffset.Parse(date));
            }
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
                case "??":
                    return ChangeType.Untracked;
                default:
                    break;
            }

            throw new InvalidOperationException("Unsupported status " + status);
        }
    }
}
