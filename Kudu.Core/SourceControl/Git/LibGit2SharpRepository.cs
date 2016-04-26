using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Kudu.Contracts.Settings;
using Kudu.Core.Tracing;
using System.IO;
using Kudu.Core.Infrastructure;
using LibGit2Sharp;
using Kudu.Contracts.Tracing;
using System.Text.RegularExpressions;

namespace Kudu.Core.SourceControl.Git
{
    public class LibGit2SharpRepository : IGitRepository
    {
        private const string _remoteAlias = GitExeRepository.RemoteAlias;
        private readonly ITraceFactory _tracerFactory;
        private readonly IDeploymentSettingsManager _settings;
        private readonly GitExeRepository _legacyGitExeRepository;

        public LibGit2SharpRepository(IEnvironment environment, IDeploymentSettingsManager settings, ITraceFactory tracerFactory)
        {
            _tracerFactory = tracerFactory;
            _settings = settings;
            RepositoryPath = environment.RepositoryPath;
            _legacyGitExeRepository = new GitExeRepository(environment, settings, tracerFactory);
        }

        public string CurrentId
        {
            get
            {
                using (var repo = new LibGit2Sharp.Repository(RepositoryPath))
                {
                    var headTip = repo.Head.Tip;

                    return headTip == null ? null : headTip.Sha;
                }
            }
        }

        public string RepositoryPath { get; private set; }

        public RepositoryType RepositoryType
        {
            get
            {
                return RepositoryType.Git;
            }
        }

        public void Initialize()
        {
            var tracer = _tracerFactory.GetTracer();
            using (tracer.Step("LibGit2SharpRepository Initialize"))
            {
                var dotGitPath = LibGit2Sharp.Repository.Init(RepositoryPath);
                using (var repo = new LibGit2Sharp.Repository(dotGitPath))
                {
                    repo.Config.Set("core.autocrlf", true);

                    // This speeds up git operations like 'git checkout', especially on slow drives like in Azure
                    repo.Config.Set("core.preloadindex", true);

                    repo.Config.Set("user.name", _settings.GetGitUsername());
                    repo.Config.Set("user.email", _settings.GetGitEmail());

                    // This is needed to make lfs work
                    repo.Config.Set("filter.lfs.clean", "git-lfs clean %f");
                    repo.Config.Set("filter.lfs.smudge", "git-lfs smudge %f");
                    repo.Config.Set("filter.lfs.required", true);

                    using (tracer.Step("Configure git server"))
                    {
                        // Allow getting pushes even though we're not bare
                        repo.Config.Set("receive.denyCurrentBranch", "ignore");
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

                        File.WriteAllText(Path.Combine(dotGitPath, "web.config"), content);
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

                        repo.Config.Set("credential.helper", string.Format("!'{0}'", GitCredentialHookPath));
                    }
                }
                using (tracer.Step("Setup post receive hook"))
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

        private string GitCredentialHookPath
        {
            get
            {
                return Path.Combine(RepositoryPath, ".git", "hooks", "git-credential-invalid.sh");
            }
        }

        public ChangeSet GetChangeSet(string id)
        {
            using (var repo = new LibGit2Sharp.Repository(RepositoryPath))
            {
                LibGit2Sharp.Commit commit = repo.Lookup<LibGit2Sharp.Commit>(id);

                if (commit == null)
                {
                    return null;
                }

                return new ChangeSet(commit.Sha, commit.Author.Name, commit.Author.Email, commit.Message, commit.Author.When);
            }
        }

        public void AddFile(string path)
        {
            using (var repo = new LibGit2Sharp.Repository(RepositoryPath))
            {
                repo.Stage(path);
            }
        }

        public bool Commit(string message, string authorName, string emailAddress)
        {
            using (var repo = new LibGit2Sharp.Repository(RepositoryPath))
            {
                var changes = repo.RetrieveStatus(new StatusOptions
                {
                    DetectRenamesInIndex = false,
                    DetectRenamesInWorkDir = false
                }).Select(c => c.FilePath);

                if (!changes.Any())
                {
                    return false;
                }

                repo.Stage(changes);
                if (string.IsNullOrWhiteSpace(authorName) ||
                    string.IsNullOrWhiteSpace(emailAddress))
                {
                    repo.Commit(message);
                }
                else
                {
                    repo.Commit(message, new Signature(authorName, emailAddress, DateTimeOffset.UtcNow));
                }
                return true;
            }
        }

        public void Update(string id)
        {
            using (var repo = new LibGit2Sharp.Repository(RepositoryPath))
            {
                repo.Checkout(id, new CheckoutOptions { CheckoutModifiers = CheckoutModifiers.Force });
            }
        }

        public void Update()
        {
            Update("master");
        }

        public void Push()
        {
            using (var repo = new LibGit2Sharp.Repository(RepositoryPath))
            {
                var masterBranch = repo.Branches["master"];
                repo.Network.Push(masterBranch);
            }
        }

        public void FetchWithoutConflict(string remoteUrl, string branchName)
        {
            var tracer = _tracerFactory.GetTracer();
            try
            {
                using (var repo = new LibGit2Sharp.Repository(RepositoryPath))
                {
                    var trackedBranchName = string.Format("{0}/{1}", _remoteAlias, branchName);
                    var refSpec = string.Format("+refs/heads/{0}:refs/remotes/{1}", branchName, trackedBranchName);

                    // Remove it if it already exists (does not throw if it doesn't)
                    repo.Network.Remotes.Remove(_remoteAlias);

                    // Configure the remote
                    var remote = repo.Network.Remotes.Add(_remoteAlias, remoteUrl, refSpec);

                    using (tracer.Step("LibGit2SharpRepository Fetch"))
                    {
                        // This will only retrieve the "master"
                        repo.Network.Fetch(remote);
                    }

                    using (tracer.Step("LibGit2SharpRepository Update"))
                    {
                        // Optionally set up the branch tracking configuration
                        var trackedBranch = repo.Branches[trackedBranchName];
                        if (trackedBranch == null)
                        {
                            throw new BranchNotFoundException(branchName, null);
                        }

                        var branch = repo.Branches[branchName] ?? repo.CreateBranch(branchName, trackedBranch.Tip);
                        repo.Branches.Update(branch,
                            b => b.TrackedBranch = trackedBranch.CanonicalName);

                        // Update the raw ref to point the head of branchName to the latest fetched branch
                        UpdateRawRef(string.Format("refs/heads/{0}", branchName), trackedBranchName);

                        // Now checkout out our branch, which points to the right place
                        Update(branchName);
                    }
                }
            }
            catch (LibGit2SharpException exception)
            {
                // LibGit2Sharp doesn't support SSH yet. Use GitExeRepository
                // LibGit2Sharp only supports smart Http protocol
                if (exception.Message.Equals("Unsupported URL protocol") ||
                    exception.Message.Equals("Received unexpected content-type"))
                {
                    tracer.TraceWarning("LibGit2SharpRepository fallback to git.exe due to {0}", exception.Message);

                    _legacyGitExeRepository.FetchWithoutConflict(remoteUrl, branchName);
                }
                else
                {
                    throw;
                }
            }
        }

        public void Clean()
        {
            using (var repo = new LibGit2Sharp.Repository(RepositoryPath))
            {
                repo.RemoveUntrackedFiles();
            }
        }

        public void CreateOrResetBranch(string branchName, string startPoint)
        {
            using (var repo = new LibGit2Sharp.Repository(RepositoryPath))
            {
                if (string.IsNullOrWhiteSpace(startPoint))
                {
                    var branch = repo.GetOrCreateBranch(branchName);
                    repo.Checkout(branch);
                }
                else
                {
                    var commit = repo.Lookup<Commit>(startPoint);
                    if (commit == null)
                    {
                        throw new LibGit2Sharp.NotFoundException(string.Format("Start point \"{0}\" for reset was not found.", startPoint));
                    }
                    var branch = repo.GetOrCreateBranch(branchName);
                    repo.Checkout(branch);
                    repo.Reset(ResetMode.Hard, commit);
                }
            }
        }

        public void UpdateRef(string toBeUpdatedToAlias)
        {
            UpdateRawRef(string.Format("refs/heads/{0}", "master"), string.Format("refs/heads/{0}", toBeUpdatedToAlias));
        }

        public void UpdateRawRef(string toBeUpdatedRef, string toBeUpdatedToRef)
        {
            using (var repo = new LibGit2Sharp.Repository(RepositoryPath))
            {
                repo.Refs.UpdateTarget(toBeUpdatedRef, toBeUpdatedToRef);
            }
        }

        public bool DoesBranchContainCommit(string branchName, string commitOrBranchName)
        {
            using (var repo = new LibGit2Sharp.Repository(RepositoryPath))
            {
                var branch = repo.Branches[branchName];
                if (branch == null) return false;
                return repo.Refs.ReachableFrom(
                              new[] { repo.Refs[branch.CanonicalName] },
                              new[] { repo.Lookup<Commit>(commitOrBranchName) }).Any();
            }
        }

        public IEnumerable<string> ListFiles(string path, SearchOption searchOption, params string[] lookupList)
        {
            path = PathUtilityFactory.Instance.CleanPath(path);

            if (!FileSystemHelpers.IsSubfolder(RepositoryPath, path))
            {
                throw new NotSupportedException("Only paths relative to the repository root path are supported, path provided: '{0}' is not a child folder of '{1}'".FormatCurrentCulture(path, RepositoryPath));
            }

            if (!Directory.Exists(path)) return Enumerable.Empty<string>();

            using (var repo = new LibGit2Sharp.Repository(RepositoryPath))
            {
                var files = repo.Diff.Compare<TreeChanges>(null, DiffTargets.Index, lookupList, compareOptions: new CompareOptions() { IncludeUnmodified = true, Similarity = SimilarityOptions.None })
                                      .Select(d => Path.Combine(repo.Info.WorkingDirectory, d.Path))
                                      .Where(p => p.StartsWith(path, StringComparison.OrdinalIgnoreCase));

                switch (searchOption)
                {
                    case SearchOption.TopDirectoryOnly:
                        files = files.Where(line => !line.Substring(path.Length).TrimStart('\\').Contains('\\'));
                        break;

                    case SearchOption.AllDirectories:
                        break;

                    default:
                        throw new NotSupportedException("Search option {0} is not supported".FormatCurrentCulture(searchOption));
                }
                // Make sure to materialize the list before finalizing repo
                return files.ToList();
            }
        }

        private string PostReceiveHookPath
        {
            get
            {
                return Path.Combine(RepositoryPath, ".git", "hooks", "post-receive");
            }
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
                if (!SkipPostReceiveHookCheck && !File.Exists(PostReceiveHookPath))
                {
                    return false;
                }

                if (LibGit2Sharp.Repository.IsValid(RepositoryPath))
                {
                    // This should no-op if HEAD size > 0
                    // Corrupted HEAD might happen if the process was terminated during a long running git operation.
                    _legacyGitExeRepository.TryFixCorruptedGit();
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        public void UpdateSubmodules()
        {
            _legacyGitExeRepository.UpdateSubmodules();
        }

        public void ClearLock()
        {
            _legacyGitExeRepository.ClearLock();
        }

        public bool Rebase(string branchName)
        {
            return _legacyGitExeRepository.Rebase(branchName);
        }

        public void RebaseAbort()
        {
            _legacyGitExeRepository.RebaseAbort();
        }
    }

    public static class RepositoryExtensions
    {
        public static Branch GetOrCreateBranch(this Repository repo, string branchName)
        {
            return repo.Branches[branchName] ?? repo.CreateBranch(branchName);
        }
    }
}