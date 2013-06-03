using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;

namespace Kudu.Core.SourceControl.Git
{
    /// <summary>
    /// Implementation of a git repository over git.exe
    /// </summary>
    public class GitExeRepository : IRepository
    {
        private const string RemoteAlias = "external";

        // From CIT experience, this is most common flakiness issue with github.com
        private static readonly string[] RetriableFetchFailures =
        {
            "Unknown SSL protocol error in connection",
            "The requested URL returned error: 403 while accessing",
            "fatal: HTTP request failed",
            "fatal: The remote end hung up unexpectedly"
        };

        private readonly GitExecutable _gitExe;
        private readonly ITraceFactory _tracerFactory;
        private readonly IDeploymentSettingsManager _settings;

        public GitExeRepository(IEnvironment environment, IDeploymentSettingsManager settings, ITraceFactory profilerFactory)
        {
            _gitExe = new GitExecutable(environment.RepositoryPath, settings.GetCommandIdleTimeout());
            _tracerFactory = profilerFactory;
            _settings = settings;
            SkipPostReceiveHookCheck = false;
            if (!String.IsNullOrEmpty(environment.SiteRootPath))
            {
                _gitExe.SetHomePath(environment.SiteRootPath);
            }
        }

        public string CurrentId
        {
            get
            {
                return Resolve("HEAD");
            }
        }

        public string RepositoryPath
        {
            get { return _gitExe.WorkingDirectory; }
        }

        public RepositoryType RepositoryType
        {
            get { return RepositoryType.Git; }
        }

        public bool SkipPostReceiveHookCheck
        {
            get;
            set;
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
                    string output = _gitExe.Execute("rev-parse --git-dir").Item1;
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

        private string PostReceiveHookPath
        {
            get
            {
                return Path.Combine(_gitExe.WorkingDirectory, ".git", "hooks", "post-receive");
            }
        }

        private string GitCredentialHookPath
        {
            get
            {
                return Path.Combine(_gitExe.WorkingDirectory, ".git", "hooks", "git-credential-invalid.sh");
            }
        }

        public void Initialize()
        {
            var profiler = _tracerFactory.GetTracer();
            using (profiler.Step("GitExeRepository.Initialize"))
            {
                _gitExe.Execute(profiler, "init");

                _gitExe.Execute(profiler, "config core.autocrlf true");

                // This speeds up git operations like 'git checkout', especially on slow drives like in Azure
                _gitExe.Execute(profiler, "config core.preloadindex true");

                _gitExe.Execute(profiler, @"config user.name ""{0}""", _settings.GetGitUsername());

                _gitExe.Execute(profiler, @"config user.email ""{0}""", _settings.GetGitEmail());

                using (profiler.Step("Configure git server"))
                {
                    // Allow getting pushes even though we're not bare
                    _gitExe.Execute(profiler, "config receive.denyCurrentBranch ignore");
                }

                // to disallow browsing to this folder in case of in-place repo
                using (profiler.Step("Create deny users for .git folder"))
                {
                    string content = "<?xml version=\"1.0\"" + @"?>
<configuration>
  <system.web>
    <authorization>
      <deny users=" + "\"*\"" + @"/>
    </authorization>
  </system.web>
<configuration>";

                    File.WriteAllText(Path.Combine(_gitExe.WorkingDirectory, ".git", "web.config"), content);
                }

                // Server env does not support interactive cred prompt; hence, we intercept any credential provision
                // for git fetch/clone with http/https scheme and return random invalid u/p forcing 'fatal: Authentication failed.'
                using (profiler.Step("Configure git-credential"))
                {
                    FileSystemHelpers.EnsureDirectory(Path.GetDirectoryName(GitCredentialHookPath));

                    string content = @"#!/bin/sh
if [ " + "\"$1\" = \"get\"" + @" ]; then
      echo username=dummyUser
      echo password=dummyPassword
fi" + "\n";

                    File.WriteAllText(GitCredentialHookPath, content);

                    _gitExe.Execute(profiler, "config credential.helper !'{0}'", GitCredentialHookPath);
                }

                using (profiler.Step("Setup post receive hook"))
                {
                    FileSystemHelpers.EnsureDirectory(Path.GetDirectoryName(PostReceiveHookPath));

                    string content = @"#!/bin/sh
read i
echo $i > pushinfo
" + KnownEnvironment.KUDUCOMMAND + "\n";

                    File.WriteAllText(PostReceiveHookPath, content);
                }

                // NOTE: don't add any new init steps after creating the post receive hook,
                // as it's also used to mark that Init was fully executed
            }
        }

        public string Resolve(string id)
        {
            return _gitExe.Execute("rev-parse {0}", id).Item1.Trim();
        }

        public ChangeSet GetChangeSet(string id)
        {
            string showCommit = _gitExe.Execute("log -n 1 {0}", id).Item1;
            var commitReader = showCommit.AsReader();
            return ParseCommit(commitReader);
        }

        public void AddFile(string path)
        {
            _gitExe.Execute("add {0}", path);
        }

        public bool Commit(string message, string authorName = null)
        {
            ITracer tracer = _tracerFactory.GetTracer();

            // Add all unstaged files
            _gitExe.Execute(tracer, "add -A");

            try
            {
                string output;
                if (authorName == null)
                {
                    output = _gitExe.Execute(tracer, "commit -m \"{0}\"", message).Item1;
                }
                else
                {
                    output = _gitExe.Execute(tracer, "commit -m \"{0}\" --author=\"{1}\"", message, authorName).Item1;
                }

                // No pending changes
                if (output.Contains("working directory clean"))
                {
                    return false;
                }

                return true;
            }
            catch (Exception e)
            {
                if (e.Message.Contains("nothing to commit"))
                {
                    return false;
                }
                throw;
            }
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

        public void FetchWithoutConflict(string remote, string branchName)
        {
            ITracer tracer = _tracerFactory.GetTracer();
            try
            {
                TryUpdateRemote(remote, RemoteAlias, branchName, tracer);

                string fetchCommand = @"fetch {0} --progress";
                if (this.IsEmpty() && _settings.AllowShallowClones())
                {
                    // If it's the initial fetch and the setting allows it, we do a shallow fetch so that we omit all the history, keeping
                    // our repo small. In subsequent fetches, we can't use the --depth flag as that would further
                    // trim the repo, preventing us from redeploying old deployments
                    fetchCommand += " --depth 1";
                }

                try
                {
                    GitFetchWithRetry(() => _gitExe.Execute(tracer, fetchCommand, RemoteAlias));
                }
                catch (CommandLineException exception)
                {
                    // Check if the fetch failed because the remote repository hasn't been set up as yet.
                    string branchNotFoundMessage = "fatal: Couldn't find remote ref";
                    string exceptionMessage = exception.Message ?? String.Empty;
                    if (exceptionMessage.StartsWith(branchNotFoundMessage, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new BranchNotFoundException(branchName, exception);
                    }
                    throw;
                }

                // Set our branch to point to the remote branch we just fetched. This is a trivial branch pointer
                // operation that doesn't touch any working files
                _gitExe.Execute(tracer, @"update-ref refs/heads/{1} {0}/{1}", RemoteAlias, branchName);

                // Now checkout out our branch, which points to the right place
                Update(branchName);
            }
            finally
            {
                TryDeleteRemote(RemoteAlias, tracer);
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
                GitFetchWithRetry(() => _gitExe.Execute(tracer, "submodule update --init --recursive"));
            }
        }

        public void CreateOrResetBranch(string branchName, string startPoint)
        {
            _gitExe.Execute("checkout -B \"{0}\" {1}", branchName, startPoint);
        }

        public bool Rebase(string branchName)
        {
            string output = _gitExe.Execute("rebase \"{0}\"", branchName).Item1;
            return output.Contains("is up to date");
        }

        public void RebaseAbort()
        {
            _gitExe.Execute("rebase --abort");
        }

        public void UpdateRef(string source)
        {
            _gitExe.Execute("update-ref refs/heads/master refs/heads/{0}", source);
        }

        public void ClearLock()
        {
            // Delete the lock file from the .git folder
            string lockFilePath = Path.Combine(_gitExe.WorkingDirectory, ".git", "index.lock");
            if (File.Exists(lockFilePath))
            {
                ITracer tracer = _tracerFactory.GetTracer();
                tracer.TraceWarning("Deleting left over index.lock file");
                FileSystemHelpers.DeleteFileSafe(lockFilePath);
            }
        }

        public void SetTraceLevel(int level)
        {
            _gitExe.SetTraceLevel(level);
        }

        public IEnumerable<string> ListFiles(string path, SearchOption searchOption, params string[] lookupList)
        {
            ITracer tracer = _tracerFactory.GetTracer();

            path = PathUtility.CleanPath(path);

            if (!path.StartsWith(RepositoryPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException("Only paths relative to the repository root path are supported, path provided: {0}".FormatCurrentCulture(path));
            }

            if (Directory.Exists(path))
            {
                // TODO: Consider an implementation where the gitExe returns the list of files as a list (not storing the files list output as a blob)
                // In-order to conserve memory consumption
                Tuple<string, string> result = _gitExe.Execute(tracer, @"ls-files {0}", String.Join(" ", lookupList), RepositoryPath);

                if (!String.IsNullOrEmpty(result.Item1))
                {
                    IEnumerable<string> lines = result.Item1.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

                    lines = lines
                        .Select(line => Path.Combine(RepositoryPath, line.Trim().Replace('/', '\\')))
                        .Where(p => p.StartsWith(path, StringComparison.OrdinalIgnoreCase));

                    switch (searchOption)
                    {
                        case SearchOption.TopDirectoryOnly:
                            lines = lines.Where(line => !line.Substring(path.Length).TrimStart('\\').Contains('\\'));
                            break;

                        case SearchOption.AllDirectories:
                            break;

                        default:
                            throw new NotSupportedException("Search option {0} is not supported".FormatCurrentCulture(searchOption));
                    }

                    return lines;
                }
            }

            return Enumerable.Empty<string>();
        }

        private bool IsEmpty()
        {
            // REVIEW: Is this reliable
            return String.IsNullOrWhiteSpace(_gitExe.Execute("branch").Item1);
        }

        private void AddRemote(string remote, string remoteAlias, string branchName, ITracer tracer)
        {
            _gitExe.Execute(tracer, @"remote add -t {2} {0} ""{1}""", remoteAlias, remote, branchName);
        }

        private void TryUpdateRemote(string remote, string remoteAlias, string branchName, ITracer tracer)
        {
            try
            {
                // Try adding a remote. In the event this fails, it might be the result of the previous remote deletion failing. 
                // In this case, delete and re-add it.
                AddRemote(remote, remoteAlias, branchName, tracer);
            }
            catch
            {
                TryDeleteRemote(remoteAlias, tracer);
                AddRemote(remote, remoteAlias, branchName, tracer);
            }
        }

        private void TryDeleteRemote(string remoteAlias, ITracer tracer)
        {
            try
            {
                _gitExe.Execute(tracer, @"remote rm {0}", remoteAlias);
            }
            catch { }
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
    }
}
