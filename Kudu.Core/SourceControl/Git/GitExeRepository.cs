using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core.Deployment;
using Kudu.Core.Deployment.Generator;
using Kudu.Core.Helpers;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;

namespace Kudu.Core.SourceControl.Git
{
    /// <summary>
    /// Implementation of a git repository over git.exe
    /// </summary>
    public class GitExeRepository : IGitRepository
    {
        internal const string RemoteAlias = "origin";

        // From CIT experience, this is most common flakiness issue with github.com
        private static readonly string[] RetriableFetchFailures =
        {
            "Unknown SSL protocol error in connection",
            "The requested URL returned error: 403 while accessing",
            "fatal: HTTP request failed",
            "fatal: The remote end hung up unexpectedly"
        };

        private static readonly string[] _lockFileNames = new[] { "index.lock", "HEAD.lock" };

        private IEnvironment _environment;
        private readonly GitExecutable _gitExe;
        private readonly ITraceFactory _tracerFactory;
        private readonly IDeploymentSettingsManager _settings;

        public GitExeRepository(IEnvironment environment, IDeploymentSettingsManager settings, ITraceFactory profilerFactory)
        {
            _gitExe = new GitExecutable(environment.RepositoryPath, settings.GetCommandIdleTimeout());
            _environment = environment;
            _tracerFactory = profilerFactory;
            _settings = settings;
            SkipPostReceiveHookCheck = false;
            _gitExe.SetHomePath(environment);
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
            var tracer = _tracerFactory.GetTracer();
            using (tracer.Step("GitExeRepository.Initialize"))
            {
                Execute(tracer, "init");

                Execute(tracer, "config core.autocrlf true");

                // This speeds up git operations like 'git checkout', especially on slow drives like in Azure
                Execute(tracer, "config core.preloadindex true");

                Execute(tracer, @"config user.name ""{0}""", _settings.GetGitUsername());
                Execute(tracer, @"config user.email ""{0}""", _settings.GetGitEmail());

                // This is needed to make lfs work
                Execute(tracer, @"config filter.lfs.clean ""git-lfs clean %f""");
                Execute(tracer, @"config filter.lfs.smudge ""git-lfs smudge %f""");
                Execute(tracer, @"config filter.lfs.required true");

                using (tracer.Step("Configure git server"))
                {
                    // Allow getting pushes even though we're not bare
                    Execute(tracer, "config receive.denyCurrentBranch ignore");
                }

                // to disallow browsing to this folder in case of in-place repo
                using (tracer.Step("Create deny users for .git folder"))
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
                using (tracer.Step("Configure git-credential"))
                {
                    FileSystemHelpers.EnsureDirectory(Path.GetDirectoryName(GitCredentialHookPath));

                    string content = @"#!/bin/sh
if [ " + "\"$1\" = \"get\"" + @" ]; then
      echo username=dummyUser
      echo password=dummyPassword
fi" + "\n";

                    File.WriteAllText(GitCredentialHookPath, content);

                    Execute(tracer, "config credential.helper \"!'{0}'\"", GitCredentialHookPath);
                }

                using (tracer.Step("Setup post receive hook"))
                {
                    FileSystemHelpers.EnsureDirectory(Path.GetDirectoryName(PostReceiveHookPath));

                    //#!/bin/sh
                    //read i
                    //echo $i > pushinfo
                    //KnownEnvironment.KUDUCOMMAND
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("#!/bin/sh");
                    sb.AppendLine("read i");
                    sb.AppendLine("echo $i > pushinfo");
                    sb.AppendLine("/usr/bin/mono " + KnownEnvironment.KUDUCOMMAND);
                    FileSystemHelpers.WriteAllText(PostReceiveHookPath, sb.ToString().Replace("\r\n", "\n"));

                    if (!OSDetecter.IsOnWindows())
                    {
                        using (tracer.Step("Non-Windows enviroment, granting full permission to post-receive hook file"))
                        {
                            var hooksDir = new DirectoryInfo(PostReceiveHookPath).Parent;
                            var exeFactory = new ExternalCommandFactory(_environment, _settings, null);
                            Executable exe = exeFactory.BuildCommandExecutable("/bin/chmod", hooksDir.FullName, _settings.GetCommandIdleTimeout(), NullLogger.Instance);
                            exe.Execute("777 {0}", PostReceiveHookPath);
                        }
                    }
                }

                // NOTE: don't add any new init steps after creating the post receive hook,
                // as it's also used to mark that Init was fully executed
            }
        }

        public string Resolve(string id)
        {
            return Execute("rev-parse {0}", id).Trim();
        }

        public ChangeSet GetChangeSet(string id)
        {
            string output = null;
            try
            {
                output = Execute("log -n 1 {0} --", id);
            }
            catch (CommandLineException ex)
            {
                if (!String.IsNullOrEmpty(ex.Message) && ex.Message.IndexOf("bad revision ", StringComparison.OrdinalIgnoreCase) != -1)
                {
                    // Indicates the changeset does not exist in the repo.
                    return null;
                }
                throw;
            }
            var commitReader = output.AsReader();
            return ParseCommit(commitReader);
        }

        public void AddFile(string path)
        {
            Execute("add {0}", path);
        }

        public bool Commit(string message, string authorName = null, string emailAddress = null)
        {
            ITracer tracer = _tracerFactory.GetTracer();

            // Add all unstaged files
            Execute(tracer, "add -A");

            try
            {
                string output;
                if (authorName == null || emailAddress == null)
                {
                    output = Execute(tracer, "commit -m \"{0}\"", message);
                }
                else
                {
                    output = Execute(tracer, "commit -m \"{0}\" --author=\"{1} <{2}>\"", message, authorName, emailAddress);
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
            Execute(@"clean -xdff");
        }

        public void Push()
        {
            Execute(@"push origin master");
        }

        public void FetchWithoutConflict(string remote, string branchName)
        {
            ITracer tracer = _tracerFactory.GetTracer();

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
                ExecuteGenericGitCommandWithRetryAndCatchingWellKnownGitErrors(() => Execute(tracer, fetchCommand, RemoteAlias));
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
            Execute(tracer, @"update-ref refs/heads/{1} {0}/{1}", RemoteAlias, branchName);

            // Now checkout out our branch, which points to the right place
            Update(branchName);
        }

        public void Update(string id)
        {
            Execute("checkout {0} --force", id);
        }

        public void Update()
        {
            Update("master");
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

                ExecuteGenericGitCommandWithRetryAndCatchingWellKnownGitErrors(() => Execute("submodule update --init --recursive"));
            }
        }

        public void CreateOrResetBranch(string branchName, string startPoint)
        {
            Execute("checkout -B \"{0}\" {1}", branchName, startPoint);
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

        public void UpdateRef(string source)
        {
            Execute("update-ref refs/heads/master refs/heads/{0}", source);
        }

        public bool DoesBranchContainCommit(string branch, string commit)
        {
            if (String.IsNullOrEmpty(branch) || String.IsNullOrEmpty(commit))
            {
                return false;
            }
            string output = Execute("branch --contains \"{0}\"", commit);
            string match = String.Format(" {0}\n", branch);
            return output.IndexOf(match, StringComparison.OrdinalIgnoreCase) != -1;
        }

        public void ClearLock()
        {
            // Delete the lock file from the .git folder
            var lockFilesPath = Path.Combine(_gitExe.WorkingDirectory, ".git");
            // Always clear lock file under ".git/refs/heads"
            var branchLockFiles = Path.Combine(_gitExe.WorkingDirectory, ".git", "refs", "heads");

            if (!Directory.Exists(lockFilesPath))
            {
                return;
            }

            List<string> lockFiles = Directory.EnumerateFiles(lockFilesPath, "*.lock", SearchOption.AllDirectories)
                                     .Where(fullPath => _lockFileNames.Contains(Path.GetFileName(fullPath), StringComparer.OrdinalIgnoreCase))
                                     .ToList();

            if (Directory.Exists(branchLockFiles))
            {
                // perform a seperated EnumerateFiles, otherwise will need to do extra string compare for every files udner .git folder and subfolder
                lockFiles.AddRange(Directory.EnumerateFiles(branchLockFiles, "*.lock", SearchOption.TopDirectoryOnly));
            }

            if (lockFiles.Count > 0)
            {
                ITracer tracer = _tracerFactory.GetTracer();
                tracer.TraceWarning("Deleting left over lock files: [{0}]", string.Join(",", lockFiles));
                foreach (var file in lockFiles)
                {
                    FileSystemHelpers.DeleteFileSafe(file);
                }
            }
        }

        public void SetTraceLevel(int level)
        {
            _gitExe.SetTraceLevel(level);
        }

        public IEnumerable<string> ListFiles(string path, SearchOption searchOption, params string[] lookupList)
        {
            path = PathUtilityFactory.Instance.CleanPath(path);

            if (!FileSystemHelpers.IsSubfolder(RepositoryPath, path))
            {
                throw new NotSupportedException("Only paths relative to the repository root path are supported, path provided: '{0}' is not a child folder of '{1}'".FormatCurrentCulture(path, RepositoryPath));
            }

            if (Directory.Exists(path))
            {
                // TODO: Consider an implementation where the gitExe returns the list of files as a list (not storing the files list output as a blob)
                // In-order to conserve memory consumption
                string output = DecodeGitLsOutput(Execute(@"ls-files {0}", String.Join(" ", lookupList), RepositoryPath));

                if (!String.IsNullOrEmpty(output))
                {
                    IEnumerable<string> lines = output.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

                    lines = lines
                        .Select(line => Path.Combine(RepositoryPath, line.Trim().Trim('"').Replace('/', '\\')))
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

        public static string DecodeGitLsOutput(string original)
        {
            string output = original;

            // When characters are represented as the octal values of its utf8 encoding
            // e.g. å becomes \303\245 in git.exe output
            if (original.Contains('"'))
            {
                output = System.Text.RegularExpressions.Regex.Unescape(original);

                byte[] rawBytes = Encoding.GetEncoding(1252).GetBytes(output);

                output = Encoding.UTF8.GetString(rawBytes);
            }

            return output;
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
        internal bool TryFixCorruptedGit()
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

        private string Execute(string arguments, params object[] args)
        {
            return Execute(_tracerFactory.GetTracer(), arguments, args);
        }

        private string Execute(ITracer tracer, string arguments, params object[] args)
        {
            return _gitExe.Execute(tracer, arguments, args).Item1;
        }

        private bool IsEmpty()
        {
            // REVIEW: Is this reliable
            return String.IsNullOrWhiteSpace(Execute("branch"));
        }

        private void AddRemote(string remote, string remoteAlias, string branchName, ITracer tracer)
        {
            Execute(tracer, @"remote add -t {2} {0} ""{1}""", remoteAlias, remote, branchName);
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
                Execute(tracer, @"remote rm {0}", remoteAlias);
            }
            catch { }
        }

        internal T ExecuteGenericGitCommandWithRetryAndCatchingWellKnownGitErrors<T>(Func<T> func)
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

            string message = messageBuilder.ToString().Trim();
            return new ChangeSet(id, author, email, message, DateTimeOffset.ParseExact(date, "ddd MMM d HH:mm:ss yyyy zzz", CultureInfo.InvariantCulture));
        }
    }
}
