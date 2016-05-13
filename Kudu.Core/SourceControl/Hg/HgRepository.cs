using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;
using Mercurial;

namespace Kudu.Core.SourceControl
{
    public class HgRepository : IRepository
    {
        private const string PATH_KEY = "Path";
        private const string RemoteAlias = "external";

        private readonly IExecutable _hgExecutable;
        private readonly ITraceFactory _traceFactory;
        private readonly string _homePath;
        private Repository _hgRepository;

        static HgRepository()
        {
            EnsureClientInitialized();
        }

        public HgRepository(string path, string homePath, IDeploymentSettingsManager settings, ITraceFactory traceFactory)
            : this(GetHgExecutable(path, settings.GetCommandIdleTimeout()), homePath, traceFactory)
        {
        }

        internal HgRepository(IExecutable executable, string homePath, ITraceFactory traceFactory)
        {
            _hgExecutable = executable;
            _homePath = homePath;
            _traceFactory = traceFactory;
        }

        public string RepositoryPath
        {
            get { return _hgExecutable.WorkingDirectory; }
        }

        public RepositoryType RepositoryType
        {
            get { return RepositoryType.Mercurial; }
        }

        public string CurrentId
        {
            get
            {
                string id = Repository.Identify();

                Changeset changeSet = Repository.Log(id).SingleOrDefault();
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
                string hgDirectory = Path.Combine(RepositoryPath, ".hg");
                return Directory.Exists(hgDirectory) &&
                       Directory.EnumerateFileSystemEntries(hgDirectory).Any();
            }
        }

        private Repository Repository
        {
            get
            {
                if (_hgRepository == null)
                {
                    _hgRepository = new Repository(RepositoryPath);
                }
                return _hgRepository;
            }
        }

        public void Initialize()
        {
            ITracer tracer = _traceFactory.GetTracer();

            using (tracer.Step("HgRepository.Initialize"))
            {
                Repository.Init();

                // to disallow browsing to this folder in case of in-place repo
                using (tracer.Step("Create deny users for .hg folder"))
                {
                    string content = "<?xml version=\"1.0\"" + @"?>
<configuration>
  <system.web>
    <authorization>
      <deny users=" + "\"*\"" + @"/>
    </authorization>
  </system.web>
<configuration>";

                    File.WriteAllText(Path.Combine(RepositoryPath, ".hg", "web.config"), content);
                }
            }
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
            int max = Repository.Tip().RevisionNumber;

            if (index > max)
            {
                return Enumerable.Empty<ChangeSet>();
            }

            int from = max - index;
            int to = Math.Max(0, from - limit);
            var spec = RevSpec.Range(from, to);
            return Repository.Log(spec).Select(CreateChangeSet);
        }

        public void UpdateSubmodules()
        {
            // TODO: Figure out if calling Update without parameters works 
        }

        public void AddFile(string path)
        {
            Repository.Add(path);
        }

        public void RevertFile(string path)
        {
            Repository.Remove(path);
        }

        public bool Commit(string message, string authorName = null, string emailAddress = null)
        {
            if (!Repository.Status().Any())
            {
                return false;
            }

            Repository.AddRemove();

            var overrideAuthor = string.Empty;
            if (!string.IsNullOrEmpty(authorName) &&
                !string.IsNullOrEmpty(emailAddress))
            {
                overrideAuthor = string.Format("{0} <{1}>", authorName, emailAddress);
            }

            var command = new CommitCommand
            {
                OverrideAuthor = overrideAuthor,
                Message = message
            };

            var id = Repository.Commit(command);
            return id != null;
        }

        public void Clone(string source)
        {
            Repository.Clone(source);
        }

        public void Update(string id)
        {
            Repository.Update(id);
        }

        public void Update()
        {
            Update("default");
        }

        public void Push()
        {
            Repository.Push();
        }

        public ChangeSet GetChangeSet(string id)
        {
            return GetChangeSet((RevSpec)id);
        }

        public void FetchWithoutConflict(string remote, string branchName)
        {
            // To get ssh to work with hg, we need to ensure ssh.exe exists in the path and the HOME environment variable is set.
            // NOTE: Although hge.exe accepts the path to ssh.exe via a --ssh parameter, it cannot handle any whitespace
            // This doesn't work for us since ssh.exe is located under Program Files in typical Kudu scenarios.
            _hgExecutable.SetHomePath(_homePath);
            string currentPath = System.Environment.GetEnvironmentVariable(PATH_KEY);
            currentPath = currentPath.TrimEnd(';') + ';' + Path.GetDirectoryName(PathUtilityFactory.Instance.ResolveSSHPath());
            _hgExecutable.EnvironmentVariables[PATH_KEY] = currentPath;

            ITracer tracer = _traceFactory.GetTracer();

            bool retried = false;
            
            // Whitespace in branch name is legal in Mercurial.
            // We need double quotes around branchName.
            string branchNameWithQuotes = "\"" + branchName + "\"";

        fetch:
            try
            {                
                _hgExecutable.Execute(tracer, "pull {0} --branch {1} --noninteractive", remote, branchNameWithQuotes, PathUtilityFactory.Instance.ResolveSSHPath());
            }
            catch (CommandLineException exception)
            {
                string branchNotFoundMessage = String.Format(CultureInfo.InvariantCulture, "abort: unknown branch '{0}'!", branchName);
                string recoverRequiredMessage = "abort: abandoned transaction found - run hg recover!";

                string exceptionMessage = (exception.Message ?? String.Empty).TrimEnd();
                if (exceptionMessage.StartsWith(branchNotFoundMessage, StringComparison.OrdinalIgnoreCase))
                {
                    throw new BranchNotFoundException(branchNameWithQuotes, exception);
                }
                else if (!retried && exceptionMessage.IndexOf(recoverRequiredMessage, StringComparison.OrdinalIgnoreCase) != -1)
                {
                    // Check if the previous fetch failed with a message to recover. 
                    retried = true;
                    _hgExecutable.Execute(tracer, "recover");
                    goto fetch;
                }
                throw;
            }
            _hgExecutable.Execute(tracer, "update --clean {0}", branchNameWithQuotes);
        }

        public void ClearLock()
        {
            // Delete the lock file from the .hg folder
            // From http://stackoverflow.com/questions/12865/mercurial-stuck-waiting-for-lock
            string lockFilePath = Path.Combine(RepositoryPath, ".hg/store/lock");
            if (File.Exists(lockFilePath))
            {
                ITracer tracer = _traceFactory.GetTracer();
                tracer.TraceWarning("Deleting left over .hg/store/lock file");
                FileSystemHelpers.DeleteFileSafe(lockFilePath);
            }
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

        public bool DoesBranchContainCommit(string branch, string commit)
        {
            throw new NotImplementedException();
        }

        public void Clean()
        {
            throw new NotSupportedException();
        }

        public static IExecutable GetHgExecutable(string repoPath, TimeSpan commandTimeout)
        {
            return new Executable(Client.ClientPath, repoPath, commandTimeout);
        }

        private ChangeSet GetChangeSet(RevSpec id)
        {
            var log = Enumerable.Empty<Changeset>();

            try
            {
                log = Repository.Log(id);
            }
            catch (MercurialExecutionException e)
            {
                // Older Mercurial (e.g. 2.4.1) were returning empty without errors when called with an unknown revision,
                // but newer versions (e.g. 2.7.1) actually fail here, so account for that possibility so we can run on both
                if (!e.Message.Contains("unknown revision")) throw;
            }
            
            var changeset = log.SingleOrDefault();
            if (changeset != null)
            {
                return CreateChangeSet(changeset);
            }
            return null;
        }

        public IEnumerable<string> ListFiles(string path, SearchOption searchOption, params string[] lookupList)
        {
            return FileSystemHelpers.ListFiles(path, searchOption, lookupList);
        }

        private ChangeSet CreateChangeSet(Mercurial.Changeset changeSet)
        {
            return new ChangeSet(changeSet.Hash, changeSet.AuthorName, changeSet.AuthorEmailAddress, changeSet.CommitMessage, new DateTimeOffset(changeSet.Timestamp));
        }

        private static bool EnsureClientInitialized()
        {
            // If Mercurial.Net can find the location of the repository via the PATH variable, let we'll use that
            if (!Client.CouldLocateClient)
            {
                Client.SetClientPath(PathUtilityFactory.Instance.ResolveHgPath());
            }
            return true;
        }
    }
}
