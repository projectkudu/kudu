using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kudu.Core.SourceControl.Git
{
    /// <summary>
    /// This file contains GitRepository functionality that is not supported by LibGit2Sharp.
    /// > git submodule
    /// > git rebase
    /// > git rebase --abort
    /// > git rev-parse --git-dir
    /// </summary>
    public abstract class BaseGitRepository : IRepository
    {
        internal const string _remoteAlias = "external";
        // From CIT experience, this is most common flakiness issue with github.com
        private static readonly string[] RetriableFetchFailures =
        {
            "Unknown SSL protocol error in connection",
            "The requested URL returned error: 403 while accessing",
            "fatal: HTTP request failed",
            "fatal: The remote end hung up unexpectedly"
        };

        internal readonly GitExecutable _gitExe;
        internal readonly ITraceFactory _tracerFactory;
        internal readonly IDeploymentSettingsManager _settings;

        private static readonly string[] _lockFileNames = new[] { "index.lock", "HEAD.lock" };

        protected BaseGitRepository(IEnvironment environment, IDeploymentSettingsManager settings, ITraceFactory profilerFactory)
        {
            _gitExe = new GitExecutable(environment.RepositoryPath, settings.GetCommandIdleTimeout());
            _tracerFactory = profilerFactory;
            _settings = settings;
            SkipPostReceiveHookCheck = false;
        }

        public void UpdateSubmodules()
        {
            // submodule update only needed when submodule added/updated
            // in case of submodule delete, the git database and .gitmodules already reflects that
            // there may be a submodule folder leftover, one could clean it manually by /scm/clean
            if (File.Exists(Path.Combine(_gitExe.WorkingDirectory, ".gitmodules")))
            {
                // in case the remote url is changed in .gitmodules for existing submodules
                // git submodule sync will update related git/config to reflect that change
                Execute("submodule sync");

                GitFetchWithRetry(() => Execute("submodule update --init --recursive"));
            }
        }

        public void ClearLock()
        {
            // Delete the lock file from the .git folder
            var lockFilesPath = Path.Combine(_gitExe.WorkingDirectory, ".git");

            if (!Directory.Exists(lockFilesPath))
            {
                return;
            }

            var lockFiles = Directory.EnumerateFiles(lockFilesPath, "*.lock", SearchOption.AllDirectories)
                                     .Where(fullPath => _lockFileNames.Contains(Path.GetFileName(fullPath), StringComparer.OrdinalIgnoreCase))
                                     .ToList();
            if (lockFiles.Count > 0)
            {
                ITracer tracer = _tracerFactory.GetTracer();
                tracer.TraceWarning("Deleting left over lock file");
                foreach (var file in lockFiles)
                {
                    FileSystemHelpers.DeleteFileSafe(file);
                }
            }
        }

        public bool Exists
        {
            get
            {
                // The last thing we do in Initialize is create the post-receive hook, so if it's not there,
                // treat the repo as incomplete, so that we'll fully initialize it again.
                if (!SkipPostReceiveHookCheck && !File.Exists(PostReceiveHookPath))
                {
                    return false;
                }

                try
                {
                    string output = ParseGitDirectory();
                    // If no exception and the output is .git (not a full directory to .git which means somewhere there's a git repository which is a parent of this directory)
                    // Then git repository directory found
                    return String.Equals(output.Trim(), ".git", StringComparison.OrdinalIgnoreCase);
                }
                catch (Exception ex)
                {
                    ITracer tracer = _tracerFactory.GetTracer();
                    tracer.TraceError(ex);

                    // If rev-parse fails for any reason, treat the repo as invalid
                    return false;
                }
            }
        }

        public bool Rebase(string branchName)
        {
            string output = Execute("rebase \"{0}\"", branchName);
            return output.Contains("is up to date");
        }

        public void RebaseAbort()
        {
            Execute("rebase --abort");
        }

        public bool SkipPostReceiveHookCheck
        {
            get;
            set;
        }

        internal T GitFetchWithRetry<T>(Func<T> func)
        {
            // 3 retries with 1s interval
            return OperationManager.Attempt(func, delayBeforeRetry: 1000, shouldRetry: ex =>
            {
                string error = ex.Message;
                if (RetriableFetchFailures.Any(retriableFailureMessage => error.Contains(retriableFailureMessage)))
                {
                    _tracerFactory.GetTracer().Trace("Retry due to {0}", error);
                    return true;
                }

                return false;
            });
        }

        protected string Execute(string arguments, params object[] args)
        {
            return Execute(_tracerFactory.GetTracer(), arguments, args);
        }

        protected string Execute(ITracer tracer, string arguments, params object[] args)
        {
            return _gitExe.Execute(tracer, arguments, args).Item1;
        }

        internal string PostReceiveHookPath
        {
            get
            {
                return Path.Combine(_gitExe.WorkingDirectory, ".git", "hooks", "post-receive");
            }
        }

        private string ParseGitDirectory()
        {
            const string revParseArgs = "rev-parse --git-dir";
            try
            {
                return Execute(revParseArgs);
            }
            catch (Exception)
            {
                if (!TryFixCorruptedGit())
                {
                    throw;
                }
            }

            return Execute(revParseArgs);
        }

        // Corrupted git where .git/HEAD exists but only contains \0.
        // we've since this issue on a few occasions but don't really understand what causes it
        private bool TryFixCorruptedGit()
        {
            var headFile = Path.Combine(_gitExe.WorkingDirectory, ".git", "HEAD");
            if (FileSystemHelpers.FileExists(headFile))
            {
                bool isCorrupted;
                using (var stream = FileSystemHelpers.OpenRead(headFile))
                {
                    isCorrupted = stream.ReadByte() == 0;
                }

                if (isCorrupted)
                {
                    ITracer tracer = _tracerFactory.GetTracer();
                    using (tracer.Step(@"Fix corrupted .git\HEAD file"))
                    {
                        FileSystemHelpers.DeleteFile(headFile);
                        Execute(tracer, "init");
                    }

                    return true;
                }
            }

            return false;
        }

        public abstract string CurrentId
        {
            get;
        }

        public abstract string RepositoryPath
        {
            get;
        }

        public abstract RepositoryType RepositoryType
        {
            get;
        }

        public abstract void Initialize();

        public abstract ChangeSet GetChangeSet(string id);

        public abstract void AddFile(string path);

        public abstract bool Commit(string message, string authorName, string emailAddress);

        public abstract void Update(string id);

        public abstract void Update();

        public abstract void Push();

        public abstract void Clean();

        public abstract void CreateOrResetBranch(string branchName, string startPoint);

        public abstract void UpdateRef(string source);

        public abstract bool DoesBranchContainCommit(string branch, string commit);

        public abstract IEnumerable<string> ListFiles(string path, SearchOption searchOption, params string[] lookupList);

        public abstract void FetchWithoutConflict(string remoteUrl, string branchName);
    }
}
