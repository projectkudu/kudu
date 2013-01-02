using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Kudu.Contracts.SourceControl;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;
using Kudu.Core.SourceControl.Git;
using Kudu.Core.Tracing;
using Mercurial;

namespace Kudu.Core.SourceControl
{
    public class HgRepository : IRepository
    {
        private const string PATH_KEY = "Path";
        private static readonly Lazy<bool> _lazyAction = new Lazy<bool>(EnsureClientInitialized);

        private readonly Repository _repository;
        private readonly Executable _hgExecutable;
        private readonly ITraceFactory _traceFactory;
        private readonly string _homePath;

        public HgRepository(string path, string homePath, ITraceFactory traceFactory)
        {
            _hgExecutable = new Executable(PathUtility.ResolveHgPath(), path);

            // Ensure that the client path is set for the Repository instance
            _lazyAction.Value.ToString();
            _repository = new Repository(path);
            _homePath = homePath;
            _traceFactory = traceFactory;
        }

        public string RepositoryPath
        {
            get { return _repository.Path; }
        }

        public RepositoryType RepositoryType
        {
            get { return RepositoryType.Mercurial; }
        }

        public string CurrentId
        {
            get
            {
                string id = _repository.Identify();

                Changeset changeSet = _repository.Log(id).SingleOrDefault();
                if (changeSet != null)
                {
                    // Get the full hash
                    return changeSet.Hash;
                }
                return null;
            }
        }

        public bool Exists
        {
            get
            {
                string hgDirectory = Path.Combine(_repository.Path, ".hg");
                return Directory.Exists(hgDirectory) &&
                       Directory.EnumerateFileSystemEntries(hgDirectory).Any();
            }
        }

        public void Initialize(RepositoryConfiguration configuration)
        {
            _repository.Init();
        }

        public IEnumerable<FileStatus> GetStatus()
        {
            return from fileStatus in _repository.Status()
                   let path = fileStatus.Path.Replace('\\', '/')
                   select new FileStatus(path, Convert(fileStatus.State));
        }

        public IEnumerable<ChangeSet> GetChanges()
        {
            const int bufferSize = 10;

            int index = 0;
            bool some = false;

            do
            {
                some = false;
                foreach (var changeSet in GetChanges(index, bufferSize))
                {
                    some = true;
                    yield return changeSet;
                }

                index += bufferSize;
            } while (some);
        }

        public IEnumerable<ChangeSet> GetChanges(int index, int limit)
        {
            int max = _repository.Tip().RevisionNumber;

            if (index > max)
            {
                return Enumerable.Empty<ChangeSet>();
            }

            int from = max - index;
            int to = Math.Max(0, from - limit);
            var spec = RevSpec.Range(from, to);
            return _repository.Log(spec).Select(CreateChangeSet);
        }

        public ChangeSetDetail GetDetails(string id)
        {
            var changeSet = GetChangeSet(id);

            var detail = new ChangeSetDetail(changeSet);

            return PopulateDetails(id, detail);
        }

        public ChangeSetDetail GetWorkingChanges()
        {
            var status = GetStatus().ToList();
            if (!status.Any())
            {
                return null;
            }

            _repository.AddRemove();
            var detail = PopulateDetails(null, new ChangeSetDetail());

            foreach (var fileStatus in status)
            {
                FileInfo info;
                if (detail.Files.TryGetValue(fileStatus.Path, out info))
                {
                    info.Status = fileStatus.Status;
                }
            }

            return detail;
        }

        public void UpdateSubmodules()
        {
            // TODO: Figure out if calling Update without parameters works 
        }

        public void AddFile(string path)
        {
            _repository.Add(path);
        }

        public void RevertFile(string path)
        {
            _repository.Remove(path);
        }

        public ChangeSet Commit(string message, string authorName = "")
        {
            if (!GetStatus().Any())
            {
                return null;
            }

            _repository.AddRemove();

            var command = new CommitCommand
            {
                OverrideAuthor = authorName ?? String.Empty,
                Message = message
            };

            var id = _repository.Commit(command);

            // TODO: Figure out why is id null
            id = id ?? CurrentId;

            return GetChangeSet(id);
        }

        public void Clone(string source)
        {
            _repository.Clone(source);
        }

        public void Update(string id)
        {
            _repository.Update(id);
        }

        public void Update()
        {
            Update("default");
        }

        public IEnumerable<Branch> GetBranches()
        {
            string currentId = CurrentId;
            return _repository.Branches()
                              .Select(b => ConvertToBranch(b, currentId));
        }

        private HgBranch ConvertToBranch(BranchHead branch, string activeBranchId)
        {
            string id = GetChangeSet(branch.RevisionNumber).Id;
            return new HgBranch(id, branch.Name, String.Equals(id, activeBranchId, StringComparison.OrdinalIgnoreCase));
        }

        public void Push()
        {
            _repository.Push();
        }

        public ChangeSet GetChangeSet(string id)
        {
            return GetChangeSet((RevSpec)id);
        }

        public void FetchWithoutConflict(string remote, string remoteAlias, string branchName)
        {
            // To get ssh to work with hg, we need to ensure ssh.exe exists in the path and the HOME environment variable is set.
            // NOTE: Although hge.exe accepts the path to ssh.exe via a --ssh parameter, it cannot handle any whitespace
            // This doesn't work for us since ssh.exe is located under Program Files in typical Kudu scenarios.
            _hgExecutable.SetHomePath(_homePath);
            string currentPath = System.Environment.GetEnvironmentVariable(PATH_KEY);
            currentPath = currentPath.TrimEnd(';') + ';' + Path.GetDirectoryName(PathUtility.ResolveSSHPath());
            _hgExecutable.EnvironmentVariables[PATH_KEY] = currentPath;

            ITracer tracer = _traceFactory.GetTracer();
            _hgExecutable.Execute(tracer, "pull {0} --branch {1}", remote, branchName, PathUtility.ResolveSSHPath());
            _hgExecutable.Execute(tracer, "update --clean {0}", branchName);
        }

        public void ClearLock()
        {
            // Delete the lock file from the .git folder
            // From http://stackoverflow.com/questions/12865/mercurial-stuck-waiting-for-lock
            string lockFilePath = Path.Combine(_repository.Path, ".hg/store/lock");
            if (File.Exists(lockFilePath))
            {
                ITracer tracer = _traceFactory.GetTracer();
                tracer.TraceWarning("Deleting left over .hg/store/lock file");
                FileSystemHelpers.DeleteFileSafe(lockFilePath);
            }
        }

        public ReceiveInfo GetReceiveInfo()
        {
            throw new NotSupportedException();
        }

        public void CreateOrResetBranch(string branchName, string startPoint)
        {
            throw new NotSupportedException();
        }

        public bool Rebase(string branchName)
        {
            throw new NotSupportedException();
        }

        public void RebaseAbort()
        {
            throw new NotSupportedException();
        }

        public void UpdateRef(string source)
        {
            throw new NotSupportedException();
        }

        public void Clean()
        {
            throw new NotSupportedException();
        }

        private ChangeSet GetChangeSet(RevSpec id)
        {
            var log = _repository.Log(id);
            return CreateChangeSet(log.SingleOrDefault());
        }

        internal ChangeSetDetail PopulateDetails(string id, ChangeSetDetail detail)
        {
            var summaryCommand = new DiffCommand
            {
                SummaryOnly = true
            };

            if (!String.IsNullOrEmpty(id))
            {
                summaryCommand.ChangeIntroducedByRevision = id;
            }

            IStringReader summaryReader = _repository.Diff(summaryCommand).AsReader();

            ParseSummary(summaryReader, detail);

            var diffCommand = new DiffCommand
            {
                UseGitDiffFormat = true,
            };

            if (!String.IsNullOrEmpty(id))
            {
                diffCommand.ChangeIntroducedByRevision = id;
            }

            var diffReader = _repository.Diff(diffCommand).AsReader();

            GitExeRepository.ParseDiffAndPopulate(diffReader, detail);

            return detail;
        }

        internal static void ParseSummary(IStringReader reader, ChangeSetDetail detail)
        {
            while (!reader.Done)
            {
                string line = reader.ReadLine();
                if (line.Contains('|'))
                {
                    string[] parts = line.Split('|');
                    string path = parts[0].Trim();

                    // TODO: Figure out a way to get this information
                    detail.Files[path] = new FileInfo();
                }
                else
                {
                    // n files changed, n insertions(+), n deletions(-)
                    ParserHelpers.ParseSummaryFooter(line, detail);
                }
            }
        }

        private ChangeSet CreateChangeSet(Mercurial.Changeset changeSet)
        {
            return new ChangeSet(changeSet.Hash, changeSet.AuthorName, changeSet.AuthorEmailAddress, changeSet.CommitMessage, new DateTimeOffset(changeSet.Timestamp));
        }

        internal static ChangeType Convert(FileState state)
        {
            switch (state)
            {
                case FileState.Added:
                    return ChangeType.Added;
                case FileState.Modified:
                    return ChangeType.Modified;
                case FileState.Removed:
                    return ChangeType.Deleted;
                case FileState.Unknown:
                    return ChangeType.Untracked;
                case FileState.Clean:
                case FileState.Ignored:
                case FileState.Missing:
                default:
                    break;
            }

            throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture,
                                                              Resources.Error_UnsupportedStatus,
                                                              state));
        }

        private static bool EnsureClientInitialized()
        {
            Client.SetClientPath(PathUtility.ResolveHgPath());
            return true;
        }

    }
}
