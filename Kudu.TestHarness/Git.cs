using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Kudu.Client.Deployment;
using Kudu.Core.Infrastructure;
using Kudu.Core.SourceControl.Git;
using SystemEnvironment = System.Environment;

namespace Kudu.TestHarness
{
    public static class Git
    {
        public static string Push(string repositoryPath, string url, string localBranchName = "master", string remoteBranchName = "master")
        {
            using (new LatencyLogger("git push " + repositoryPath))
            {
                Executable gitExe = GetGitExe(repositoryPath);

                string stdErr = null;

                if (localBranchName.Equals("master"))
                {
                    stdErr = GitExecute(gitExe, "push {0} {1}", url, remoteBranchName).Item2;
                }
                else
                {
                    // Checkout the local branch, making sure it points to the correct origin branch
                    GitExecute(gitExe, "checkout -B {0} origin/{0}", localBranchName);

                    // Dump out the error stream (git curl verbose)
                    stdErr = GitExecute(gitExe, "push {0} {1}:{2}", url, localBranchName, remoteBranchName).Item2;
                }

                return stdErr;
            }
        }

        public static string Id(string repositoryPath)
        {
            Executable gitExe = GetGitExe(repositoryPath);
            return GitExecute(gitExe, "rev-parse {0}", "HEAD").Item1.Trim();
        }

        public static TestRepository Init(string repositoryPath)
        {
            Executable gitExe = GetGitExe(repositoryPath);
            GitExecute(gitExe, "init");

            return new TestRepository(repositoryPath);
        }

        public static void Revert(string repositoryPath, string commit = "HEAD")
        {
            Executable gitExe = GetGitExe(repositoryPath);
            GitExecute(gitExe, "revert --no-edit \"{0}\"", commit);
        }

        public static void Reset(string repositoryPath, string commit = "HEAD^")
        {
            Executable gitExe = GetGitExe(repositoryPath);
            GitExecute(gitExe, "reset --hard \"{0}\"", commit);
        }

        public static void CheckOut(string repositoryPath, string branchName)
        {
            Executable gitExe = GetGitExe(repositoryPath);
            GitExecute(gitExe, "checkout -b {0} -t origin/{0}", branchName);
        }

        public static void Commit(string repositoryPath, string message)
        {
            Executable gitExe = GetGitExe(repositoryPath);
            try
            {
                GitExecute(gitExe, "add -A", message);
                GitExecute(gitExe, "commit -m \"{0}\"", message);
            }
            catch (Exception ex)
            {
                // Swallow exceptions on commit, since things like changing line endings
                // show up as an error
                TestTracer.Trace("Commit failed with {0}", ex);
            }

            // Verify that the commit did go thru
            string lastCommitMessage = GitExecute(gitExe, "log --oneline -1").Item1;
            if (lastCommitMessage == null || !lastCommitMessage.Contains(message))
            {
                throw new InvalidOperationException(String.Format("Mismatched commit message '{0}' != '{1}'", lastCommitMessage, message));
            }
        }

        public static void Add(string repositoryPath, string path)
        {
            Executable gitExe = GetGitExe(repositoryPath);
            GitExecute(gitExe, "add \"{0}\"", path);
        }

        public static TestRepository Clone(string repositoryName, string source = null, IDictionary<string, string> environments = null, bool noCache = false)
        {
            string commitId = null;

            if (source == null)
            {
                TestRepositoryInfo repoInfo = TestRepositories.Get(repositoryName);
                source = repoInfo.Url;
                commitId = repoInfo.CommitId;
            }

            return OperationManager.Attempt(() => CloneInternal(repositoryName, source, commitId, environments, noCache));
        }

        private static TestRepository CloneInternal(string repositoryName, string source, string commitId, IDictionary<string, string> environments, bool noCache)
        {
            // Check if we have a cached instance of the repository available locally
            string cachedPath = noCache ? null : CreateCachedRepo(source, commitId, environments);

            if (cachedPath != null)
            {
                return new TestRepository(cachedPath, obliterateOnDispose: false);
            }

            string repositoryPath = GetRepositoryPath(repositoryName);
            source = cachedPath ?? source;
            PathHelper.EnsureDirectory(repositoryPath);
            Executable gitExe = GetGitExe(repositoryPath, environments);
            GitExecute(gitExe, "clone \"{0}\" .", source);

            return new TestRepository(repositoryPath, obliterateOnDispose: true);
        }

        private static string CreateCachedRepo(string source, string commitId, IDictionary<string, string> environments)
        {
            string cachedPath = null;

            if (source.IndexOf("github.com", StringComparison.OrdinalIgnoreCase) != -1)
            {
                // If we're allowed to cache the repository, check if it already exists. If not clone it.
                string repoName = Path.GetFileNameWithoutExtension(source.Split('/').Last());

                // repo cached per test class to support test parallel run
                var context = TestContext.Current;
                if (context != null)
                {
                    repoName = GetRepoNamePerContext(context, repoName);
                }

                cachedPath = Path.Combine(PathHelper.RepositoryCachePath, repoName);

                // Check for the actually .git folder, in case some bogus parent exists but is not an actual repo
                bool alreadyExists = Directory.Exists(Path.Combine(cachedPath, ".git")) && IsGitRepo(cachedPath);

                Executable gitExe = GetGitExe(cachedPath, environments);

                if (alreadyExists)
                {

                    TestTracer.Trace("Using cached copy at location {0}", cachedPath);

                    // Get it into a clean state on the correct commit id
                    try
                    {
                        // If we don't have a specific commit id to go to, use origin/master
                        if (commitId == null)
                        {
                            commitId = "origin/master";
                        }

                        GitExecute(gitExe, "reset --hard " + commitId);
                    }
                    catch (Exception e)
                    {
                        // Some repos like Drupal don't use a master branch (e.g. default branch is 7.x). In those cases,
                        // simply reset to the HEAD. That won't undo any test commits, but at least it does some cleanup.
                        if (e.Message.Contains("ambiguous argument 'origin/master'"))
                        {
                            GitExecute(gitExe, "reset --hard HEAD");
                        }
                        else if (e.Message.Contains("unknown revision"))
                        {
                            // If that commit id doesn't exist, try fetching and doing the reset again
                            // The reason we don't initially fetch is to avoid the network hit when not necessary
                            GitExecute(gitExe, "fetch origin");
                            GitExecute(gitExe, "reset --hard " + commitId);
                        }
                        else
                        {
                            throw;
                        }
                    }
                    GitExecute(gitExe, "clean -dxf");
                }
                else
                {
                    // Delete any leftover, ignoring errors
                    FileSystemHelpers.DeleteDirectorySafe(cachedPath);

                    TestTracer.Trace("Could not find a cached copy at {0}. Cloning from source {1}.", cachedPath, source);
                    PathHelper.EnsureDirectory(cachedPath);
                    GitExecute(gitExe, "clone \"{0}\" .", source);

                    // If we have a commit id, reset to it in case it's older than the latest on our clone
                    if (commitId != null)
                    {
                        GitExecute(gitExe, "reset --hard " + commitId);
                    }
                }
            }
            return cachedPath;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string GetRepoNamePerContext(TestContext context, string repoName)
        {
            var className = context.Test.TestCase.TestMethod.TestClass.Class.Name;
            return String.Format("{0}_{1}", repoName, className.Substring(className.LastIndexOf(".") + 1));
        }

        public static string CloneToLocal(string cloneUri, string path = null)
        {
            string repositoryPath = path ?? Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(repositoryPath);
            var exe = new GitExecutable(repositoryPath, idleTimeout: TimeSpan.FromSeconds(3600));
            // reduce noises in log
            //exe.SetTraceLevel(2);
            //exe.SetHttpVerbose(true);
            exe.SetSSLNoVerify(true);
            GitExecute(exe, "clone \"{0}\" .", cloneUri);
            return repositoryPath;
        }

        public static GitDeploymentResult GitDeploy(string kuduServiceUrl, string localRepoPath, string remoteRepoUrl, string localBranchName, string remoteBranchName)
        {
            var deploymentManager = new RemoteDeploymentManager(kuduServiceUrl); 
            return GitDeploy(deploymentManager, kuduServiceUrl, localRepoPath, remoteRepoUrl, localBranchName, remoteBranchName);
        }

        public static GitDeploymentResult GitDeploy(RemoteDeploymentManager deploymentManager, string kuduServiceUrl, string localRepoPath, string remoteRepoUrl, string localBranchName, string remoteBranchName)
        {
            HttpUtils.WaitForSite(kuduServiceUrl);
            Stopwatch sw = Stopwatch.StartNew();
            string trace = Git.Push(localRepoPath, remoteRepoUrl, localBranchName, remoteBranchName);
            sw.Stop();

            return new GitDeploymentResult
            {
                GitTrace = trace,
                TotalResponseTime = sw.Elapsed
            };
        }

        public static string GetRepositoryPath(string repositoryPath)
        {
            if (Path.IsPathRooted(repositoryPath))
            {
                return repositoryPath;
            }

            return Path.Combine(PathHelper.LocalRepositoriesDir, repositoryPath);
        }

        private static Executable GetGitExe(string repositoryPath, IDictionary<string, string> environments = null)
        {
            if (!Path.IsPathRooted(repositoryPath))
            {
                repositoryPath = Path.Combine(PathHelper.LocalRepositoriesDir, repositoryPath);
            }

            FileSystemHelpers.EnsureDirectory(repositoryPath);

            // Use a really long idle timeout, since it's mostly meaningful when running on server, not client
            var exe = new GitExecutable(repositoryPath, idleTimeout: TimeSpan.FromSeconds(3600));
            // reduce noises in log
            //exe.SetTraceLevel(2);
            //exe.SetHttpVerbose(true);
            exe.SetSSLNoVerify(true);

            if (environments != null)
            {
                foreach (var pair in environments)
                {
                    exe.EnvironmentVariables.Add(pair);
                }
            }

            return exe;
        }

        private static Tuple<string, string> GitExecute(Executable gitExe, string commandFormat, params object[] args)
        {
            var command = String.Format(commandFormat, args);
            TestTracer.Trace("Executing: git {0}", command);
            Tuple<string, string> result = gitExe.Execute(command);
            TestTracer.Trace("  stdout: {0}", result.Item1);
            TestTracer.Trace("  stderr: {0}", result.Item2);
            return result;
        }

        private static bool IsGitRepo(string cachedPath)
        {
            try
            {
                // Attempt to read the HEAD commit id. If this works, 
                // it should give us enough confidence that the clone worked.
                return Git.Id(cachedPath) != null;
            }
            catch
            {
                
            }
            return false;
        }
    }
}
