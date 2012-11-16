using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Kudu.Contracts.Tracing;
using Kudu.Core.Deployment;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;

namespace Kudu.Core.SourceControl.Git
{
    /// <summary>
    /// Implementation of a git repository over git.exe
    /// </summary>
    public class GitExeRepository : IRepository
    {
        private readonly GitExecutable _gitExe;
        private readonly ITraceFactory _tracerFactory;

        public GitExeRepository(string path)
            : this(path, null, NullTracerFactory.Instance)
        {
        }

        public GitExeRepository(string path, string homePath, ITraceFactory profilerFactory)
        {
            _gitExe = new GitExecutable(path);
            _tracerFactory = profilerFactory;

            if (!String.IsNullOrEmpty(homePath))
            {
                _gitExe.SetHomePath(homePath);
            }
        }

        public string CurrentId
        {
            get
            {
                return Resolve("HEAD");
            }
        }

        public void Initialize()
        {
            var profiler = _tracerFactory.GetTracer();
            using (profiler.Step("GitExeRepository.Initialize"))
            {
                _gitExe.Execute(profiler, "init");

                _gitExe.Execute(profiler, "config core.autocrlf true");
            }
        }

        public string Resolve(string id)
        {
            return _gitExe.Execute("rev-parse {0}", id).Item1.Trim();
        }

        public IEnumerable<FileStatus> GetStatus()
        {
            string status = _gitExe.Execute("status --porcelain").Item1;
            return ParseStatus(status.AsReader());
        }

        public IEnumerable<ChangeSet> GetChanges()
        {
            return Log();
        }

        public ChangeSet GetChangeSet(string id)
        {
            string showCommit = _gitExe.Execute("show {0} -m --name-status", id).Item1;
            var commitReader = showCommit.AsReader();
            return ParseCommit(commitReader);
        }

        public IEnumerable<ChangeSet> GetChanges(int index, int limit)
        {
            return Log("log --all --skip {0} -n {1}", index, limit);
        }

        public void AddFile(string path)
        {
            _gitExe.Execute("add {0}", path);
        }

        public void RevertFile(string path)
        {
            if (IsEmpty())
            {
                _gitExe.Execute("rm --cached \"{0}\"", path);
            }
            else
            {
                try
                {
                    _gitExe.Execute("reset HEAD \"{0}\"", path);
                }
                catch
                {
                    // This command returns a non zero exit code even when it succeeds
                }
            }

            // Now get the status of the file
            var fileStatuses = GetStatus().ToDictionary(fs => fs.Path, fs => fs.Status);

            ChangeType status;
            if (fileStatuses.TryGetValue(path, out status) && status != ChangeType.Untracked)
            {
                _gitExe.Execute("checkout -- \"{0}\"", path);
            }
            else
            {
                // If the file is untracked, delete it
                string fullPath = Path.Combine(_gitExe.WorkingDirectory, path);
                File.Delete(fullPath);
            }
        }

        public ChangeSet Commit(string message, string authorName = null)
        {
            ITracer tracer = _tracerFactory.GetTracer();

            // Add all unstaged files
            _gitExe.Execute(tracer, "add -A");

            try
            {
                string output;
                if (authorName == null)
                {
                    output = _gitExe.Execute(tracer, "commit -m\"{0}\"", message).Item1;
                }
                else
                {
                    output = _gitExe.Execute("commit -m\"{0}\" --author=\"{1}\"", message, authorName).Item1;
                }

                // No pending changes
                if (output.Contains("working directory clean"))
                {
                    return null;
                }

                string newCommit = _gitExe.Execute(tracer, "show HEAD").Item1;

                using (tracer.Step("Parsing commit information"))
                {
                    return ParseCommit(newCommit.AsReader());
                }
            }
            catch (Exception e)
            {
                if (e.Message.Contains("nothing to commit"))
                {
                    return null;
                }
                throw;
            }
        }

        internal void Clone(string source)
        {
            _gitExe.Execute(@"clone ""{0}"" .", source);
        }

        public void Clean()
        {
            // two f to remove submodule (dir with git).
            // see https://github.com/capistrano/capistrano/issues/135
            _gitExe.Execute(@"clean -xdff");
        }

        public void Push()
        {
            _gitExe.Execute(@"push origin master");
        }

        public void FetchWithoutConflict(string remote, string remoteAlias, string branchName)
        {
            ITracer tracer = _tracerFactory.GetTracer();
            try
            {
                _gitExe.Execute(tracer, @"remote add {0} ""{1}""", remoteAlias, remote);
                _gitExe.Execute(tracer, @"fetch {0}", remoteAlias);
                Update(branchName);
                _gitExe.Execute(tracer, @"reset --hard {0}/{1}", remoteAlias, branchName);
            }
            finally
            {
                try
                {
                    _gitExe.Execute(tracer, @"remote rm {0}", remoteAlias);
                }
                catch { }
            }
        }

        public void Update(string id)
        {
            ITracer tracer = _tracerFactory.GetTracer();
            _gitExe.Execute(tracer, "checkout {0} --force", id);
        }

        public void Update()
        {
            Update("master");
        }

        public void UpdateSubmodules()
        {
            // submodule update only needed when submodule added/updated
            // in case of submodule delete, the git database and .gitmodules already reflects that
            // there may be a submodule folder leftover, one could clean it manually by /live/scm/clean
            if (File.Exists(Path.Combine(_gitExe.WorkingDirectory, ".gitmodules")))
            {
                ITracer tracer = _tracerFactory.GetTracer();
                _gitExe.Execute(tracer, "submodule update --init --recursive");
            }
        }

        public ChangeSetDetail GetDetails(string id)
        {
            string show = _gitExe.Execute("show {0} -m -p --numstat --shortstat", id).Item1;
            var detail = ParseShow(show.AsReader());

            string showStatus = _gitExe.Execute("show {0} -m --name-status --format=\"%H\"", id).Item1;
            var statusReader = showStatus.AsReader();

            // Skip the commit details
            statusReader.ReadLine();
            statusReader.SkipWhitespace();
            PopulateStatus(statusReader, detail);

            return detail;
        }

        public ChangeSetDetail GetWorkingChanges()
        {
            // Add everything so we can see a diff of the current changes            
            var statuses = GetStatus().ToList();

            if (!statuses.Any())
            {
                return null;
            }

            if (IsEmpty())
            {
                return MakeNewFileDiff(statuses);
            }

            string diff = _gitExe.Execute("diff --no-ext-diff -p --numstat --shortstat head").Item1;
            var detail = ParseShow(diff.AsReader(), includeChangeSet: false);

            foreach (var fileStatus in statuses)
            {
                FileInfo fileInfo;
                if (detail.Files.TryGetValue(fileStatus.Path, out fileInfo))
                {
                    fileInfo.Status = fileStatus.Status;
                }
            }

            return detail;
        }

        private ChangeSetDetail MakeNewFileDiff(IEnumerable<FileStatus> statuses)
        {
            var changeSetDetail = new ChangeSetDetail();
            foreach (var fileStatus in statuses)
            {
                var fileInfo = new FileInfo
                {
                    Status = fileStatus.Status
                };
                foreach (var diff in CreateDiffLines(fileStatus.Path))
                {
                    fileInfo.DiffLines.Add(diff);
                }
                changeSetDetail.Files[fileStatus.Path] = fileInfo;
                changeSetDetail.FilesChanged++;
                changeSetDetail.Insertions += fileInfo.DiffLines.Count;
            }
            return changeSetDetail;
        }

        private IEnumerable<LineDiff> CreateDiffLines(string path)
        {
            if (Directory.Exists(path))
            {
                return Enumerable.Empty<LineDiff>();
            }

            // TODO: Detect binary files
            path = Path.Combine(_gitExe.WorkingDirectory, path);
            var diff = new List<LineDiff>();
            string[] lines = File.ReadAllLines(path);
            foreach (var line in lines)
            {
                diff.Add(new LineDiff(ChangeType.Added, "+" + line));
            }
            return diff;
        }

        public IEnumerable<Branch> GetBranches()
        {
            if (IsEmpty())
            {
                yield break;
            }

            string branches = _gitExe.Execute("branch").Item1;
            var reader = branches.AsReader();
            string currentId = CurrentId;

            var branchNames = new List<string>();
            while (!reader.Done)
            {
                // * branchname
                var lineReader = reader.ReadLine().AsReader();
                lineReader.ReadUntilWhitespace();
                lineReader.SkipWhitespace();
                string branchName = lineReader.ReadToEnd();
                if (branchName.Contains("(no branch)"))
                {
                    continue;
                }
                branchNames.Add(branchName.Trim());
            }

            foreach (var branchName in branchNames)
            {
                string id = _gitExe.Execute("rev-parse {0}", branchName).Item1.Trim();
                yield return new GitBranch(id, branchName, currentId == id);
            }
        }

        public void SetTraceLevel(int level)
        {
            _gitExe.SetTraceLevel(level);
        }

        private bool IsEmpty()
        {
            // REVIEW: Is this reliable
            return String.IsNullOrWhiteSpace(_gitExe.Execute("branch").Item1);
        }

        private IEnumerable<ChangeSet> Log(string command = "log --all", params object[] args)
        {
            if (IsEmpty())
            {
                return Enumerable.Empty<ChangeSet>();
            }

            string log = _gitExe.Execute(command, args).Item1;
            return ParseCommits(log.AsReader());
        }

        private IEnumerable<ChangeSet> ParseCommits(IStringReader reader)
        {
            while (!reader.Done)
            {
                yield return ParseCommit(reader);
            }
        }

        internal static void PopulateStatus(IStringReader reader, ChangeSetDetail detail)
        {
            while (!reader.Done)
            {
                string line = reader.ReadLine();
                // Status lines contain tabs
                if (!line.Contains("\t"))
                {
                    continue;
                }
                var lineReader = line.AsReader();
                string status = lineReader.ReadUntilWhitespace();
                lineReader.SkipWhitespace();
                string name = lineReader.ReadToEnd().TrimEnd();
                lineReader.SkipWhitespace();
                FileInfo file;
                if (detail.Files.TryGetValue(name, out file))
                {
                    file.Status = ConvertStatus(status);
                }
            }
        }

        private static bool IsCommitHeader(string value)
        {
            return value.StartsWith("commit ");
        }

        private ChangeSetDetail ParseShow(IStringReader reader, bool includeChangeSet = true)
        {
            ChangeSetDetail detail = null;
            if (includeChangeSet)
            {
                detail = ParseCommitAndSummary(reader);
            }
            else
            {
                detail = new ChangeSetDetail();
                ParseSummary(reader, detail);
            }

            ParseDiffAndPopulate(reader, detail);

            return detail;
        }

        internal static void ParseDiffAndPopulate(IStringReader reader, ChangeSetDetail detail)
        {
            foreach (var diff in ParseDiff(reader))
            {
                FileInfo stats;
                if (!detail.Files.TryGetValue(diff.FileName, out stats))
                {
                    stats = new FileInfo();
                    detail.Files.Add(diff.FileName, stats);
                }

                // Set the binary flag if any of the files are binary
                bool binary = diff.Binary || stats.Binary;
                stats.Binary = binary;
                diff.Binary = binary;

                foreach (var line in diff.Lines)
                {
                    stats.DiffLines.Add(line);
                }
            }
        }

        internal static ChangeSetDetail ParseCommitAndSummary(IStringReader reader)
        {
            // Parse the changeset
            ChangeSet changeSet = ParseCommit(reader);

            var detail = new ChangeSetDetail(changeSet);

            ParseSummary(reader, detail);

            return detail;
        }

        internal static void ParseSummary(IStringReader reader, ChangeSetDetail detail)
        {
            reader.SkipWhitespace();

            while (!reader.Done)
            {
                string line = reader.ReadLine();

                if (ParserHelpers.IsSingleNewLine(line))
                {
                    break;
                }
                else if (line.Contains('\t'))
                {
                    // n	n	path
                    string[] parts = line.Split('\t');
                    int insertions;
                    Int32.TryParse(parts[0], out insertions);
                    int deletions;
                    Int32.TryParse(parts[1], out deletions);
                    string path = parts[2].TrimEnd();

                    detail.Files[path] = new FileInfo
                    {
                        Insertions = insertions,
                        Deletions = deletions,
                        Binary = parts[0] == "-" && parts[1] == "-"
                    };
                }
                else
                {
                    // n files changed, n insertions(+), n deletions(-)
                    ParserHelpers.ParseSummaryFooter(line, detail);
                }
            }
        }

        internal static IEnumerable<FileDiff> ParseDiff(IStringReader reader)
        {
            var builder = new StringBuilder();

            // If this was a merge change set then we'll parse the details out of the
            // first diff
            ChangeSetDetail merge = null;

            do
            {
                string line = reader.ReadLine();

                // If we see a new diff header then process the previous diff if any
                if ((reader.Done || IsDiffHeader(line)) && builder.Length > 0)
                {
                    if (reader.Done)
                    {
                        builder.Append(line);
                    }
                    string diffChunk = builder.ToString();
                    FileDiff diff = ParseDiffChunk(diffChunk.AsReader(), ref merge);

                    if (diff != null)
                    {
                        yield return diff;
                    }

                    builder.Clear();
                }

                if (!reader.Done)
                {
                    builder.Append(line);
                }
            } while (!reader.Done);
        }

        internal static bool IsDiffHeader(string line)
        {
            return line.StartsWith("diff --git", StringComparison.InvariantCulture);
        }

        internal static FileDiff ParseDiffChunk(IStringReader reader, ref ChangeSetDetail merge)
        {
            var diff = ParseDiffHeader(reader, merge);

            if (diff == null)
            {
                return null;
            }

            // Current diff range
            DiffRange currentRange = null;
            int? leftCounter = null;
            int? rightCounter = null;

            // Parse the file diff
            while (!reader.Done)
            {
                int? currentLeft = null;
                int? currentRight = null;
                string line = reader.ReadLine();

                if (line.Equals(@"\ No newline at end of file", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                bool isDiffRange = line.StartsWith("@@");
                ChangeType? changeType = null;

                if (line.StartsWith("+"))
                {
                    changeType = ChangeType.Added;
                    currentRight = ++rightCounter;
                    currentLeft = null;
                }
                else if (line.StartsWith("-"))
                {
                    changeType = ChangeType.Deleted;
                    currentLeft = ++leftCounter;
                    currentRight = null;
                }
                else if (IsCommitHeader(line))
                {
                    reader.PutBack(line.Length);
                    merge = ParseCommitAndSummary(reader);
                }
                else
                {
                    if (!isDiffRange)
                    {
                        currentLeft = ++leftCounter;
                        currentRight = ++rightCounter;
                    }
                    changeType = ChangeType.None;
                }

                if (changeType != null)
                {
                    var lineDiff = new LineDiff(changeType.Value, line);
                    if (!isDiffRange)
                    {
                        lineDiff.LeftLine = currentLeft;
                        lineDiff.RightLine = currentRight;
                    }

                    diff.Lines.Add(lineDiff);
                }

                if (isDiffRange)
                {
                    // Parse the new diff range
                    currentRange = DiffRange.Parse(line.AsReader());
                    leftCounter = currentRange.LeftFrom - 1;
                    rightCounter = currentRange.RightFrom - 1;
                }
            }

            return diff;
        }

        private static FileDiff ParseDiffHeader(IStringReader reader, ChangeSetDetail merge)
        {
            string fileName = ParseFileName(reader.ReadLine());
            bool binary = false;

            while (!reader.Done)
            {
                string line = reader.ReadLine();
                if (line.StartsWith("@@"))
                {
                    reader.PutBack(line.Length);
                    break;
                }
                else if (line.StartsWith("GIT binary patch"))
                {
                    binary = true;
                }
            }

            if (binary)
            {
                // Skip binary files
                reader.ReadToEnd();
            }

            var diff = new FileDiff(fileName)
            {
                Binary = binary
            };

            // Skip files from merged changesets
            if (merge != null && merge.Files.ContainsKey(fileName))
            {
                return null;
            }

            return diff;
        }

        internal static string ParseFileName(string diffHeader)
        {
            // Get rid of the diff header (git --diff)
            diffHeader = diffHeader.TrimEnd().Substring(diffHeader.IndexOf("a/"));

            // the format is always a/{file name} b/{file name}
            int mid = diffHeader.Length / 2;

            return diffHeader.Substring(0, mid).Substring(2);
        }

        internal static ChangeSet ParseCommit(IStringReader reader)
        {
            // commit hash
            reader.ReadUntilWhitespace();
            reader.SkipWhitespace();
            string id = reader.ReadUntilWhitespace();

            // Merges will have (from hash) so we're skipping that
            reader.ReadLine();

            string author = null;
            string email = null;
            string date = null;

            while (!reader.Done)
            {
                string line = reader.ReadLine();

                if (ParserHelpers.IsSingleNewLine(line))
                {
                    break;
                }

                var subReader = line.AsReader();
                string key = subReader.ReadUntil(':');
                // Skip :
                subReader.Skip();
                subReader.SkipWhitespace();
                string value = subReader.ReadToEnd().Trim();

                if (key.Equals("Author", StringComparison.OrdinalIgnoreCase))
                {
                    // Author <email>
                    var authorReader = value.AsReader();
                    author = authorReader.ReadUntil('<').Trim();
                    authorReader.Skip();
                    email = authorReader.ReadUntil('>');
                }
                else if (key.Equals("Date", StringComparison.OrdinalIgnoreCase))
                {
                    date = value;
                }
            }

            var messageBuilder = new StringBuilder();
            while (!reader.Done)
            {
                string line = reader.ReadLine();

                if (ParserHelpers.IsSingleNewLine(line))
                {
                    break;
                }
                messageBuilder.Append(line);
            }

            string message = messageBuilder.ToString();
            return new ChangeSet(id, author, email, message, DateTimeOffset.ParseExact(date, "ddd MMM d HH:mm:ss yyyy zzz", CultureInfo.InvariantCulture));
        }

        internal static IEnumerable<FileStatus> ParseStatus(IStringReader reader)
        {
            reader.SkipWhitespace();

            while (!reader.Done)
            {
                var subReader = reader.ReadLine().AsReader();
                string status = subReader.ReadUntilWhitespace().Trim();
                string path = subReader.ReadLine().Trim();
                yield return new FileStatus(path, ConvertStatus(status));
                reader.SkipWhitespace();
            }
        }

        internal static ChangeType ConvertStatus(string status)
        {
            switch (status)
            {
                case "A":
                case "AM":
                    return ChangeType.Added;
                case "M":
                case "MM":
                    return ChangeType.Modified;
                case "AD":
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

        internal class DiffRange
        {
            public int LeftFrom { get; set; }
            public int LeftTo { get; set; }
            public int RightFrom { get; set; }
            public int RightTo { get; set; }

            public static DiffRange Parse(IStringReader reader)
            {
                var range = new DiffRange();
                reader.Skip("@@");
                reader.SkipWhitespace();
                reader.Skip('-');
                range.LeftFrom = reader.ReadInt();
                if (reader.Skip(','))
                {
                    range.LeftTo = range.LeftFrom + reader.ReadInt();
                }
                else
                {
                    range.LeftTo = range.LeftFrom;
                }
                reader.SkipWhitespace();
                reader.Skip('+');
                range.RightFrom = reader.ReadInt();
                if (reader.Skip(','))
                {
                    range.RightTo = range.RightFrom + reader.ReadInt();
                }
                else
                {
                    range.RightTo = range.RightFrom;
                }
                reader.SkipWhitespace();
                reader.Skip("@@");
                return range;
            }
        }
    }
}
