using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Kudu.Client.Deployment;
using Kudu.Contracts.Settings;
using Kudu.Core.Infrastructure;
using Kudu.Core.SourceControl.Git;
using SystemEnvironment = System.Environment;

namespace Kudu.TestHarness
{
    public static class Git
    {
        public static string Push(string repositoryPath, string url, string localBranchName = "master", string remoteBranchName = "master")
        {
            Executable gitExe = GetGitExe(repositoryPath);

            string stdErr = null;

            if (localBranchName.Equals("master"))
            {
                stdErr = gitExe.Execute("push {0} {1}", url, remoteBranchName).Item2;

                // Dump out the error stream (git curl verbose)
                Debug.WriteLine(stdErr);
            }
            else
            {
                // Dump out the error stream (git curl verbose)
                stdErr = gitExe.Execute("push {0} {1}:{2}", url, localBranchName, remoteBranchName).Item2;

                Debug.WriteLine(stdErr);
            }

            return stdErr;
        }

        public static TestRepository Init(string repositoryPath)
        {
            Executable gitExe = GetGitExe(repositoryPath);
            gitExe.Execute("init");

            return new TestRepository(repositoryPath);
        }

        public static void Revert(string repositoryPath, string commit = "HEAD")
        {
            Executable gitExe = GetGitExe(repositoryPath);
            gitExe.Execute("revert --no-edit \"{0}\"", commit);
        }

        public static void Reset(string repositoryPath, string commit = "HEAD^")
        {
            Executable gitExe = GetGitExe(repositoryPath);
            gitExe.Execute("reset --hard \"{0}\"", commit);
        }

        public static void CheckOut(string repositoryPath, string branchName)
        {
            Executable gitExe = GetGitExe(repositoryPath);
            gitExe.Execute("checkout -b {0} -t origin/{0}", branchName);
        }

        public static void Commit(string repositoryPath, string message)
        {
            Executable gitExe = GetGitExe(repositoryPath);
            try
            {
                gitExe.Execute("add -A", message);
                gitExe.Execute("commit -m \"{0}\"", message);
            }
            catch (Exception ex)
            {
                // Swallow exceptions on comit, since things like changing line endings
                // show up as an error
                Debug.WriteLine(ex.Message);
            }
        }

        public static void Add(string repositoryPath, string path)
        {
            Executable gitExe = GetGitExe(repositoryPath);
            gitExe.Execute("add \"{0}\"", path);
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
            gitExe.Execute("clone \"{0}\" .", source);

            return new TestRepository(repositoryPath, obliterateOnDispose: true);
        }

        private static string CreateCachedRepo(string source, string commitId, IDictionary<string, string> environments)
        {
            string cachedPath = null;

            if (source.IndexOf("github.com", StringComparison.OrdinalIgnoreCase) != -1)
            {
                // If we're allowed to cache the repository, check if it already exists. If not clone it.
                string repoName = Path.GetFileNameWithoutExtension(source.Split('/').Last());
                cachedPath = Path.Combine(PathHelper.RepositoryCachePath, repoName);

                // Check for the actually .git folder, in case some bogus parent exists but is not an actual repo
                bool alreadyExists = Directory.Exists(Path.Combine(cachedPath, ".git"));

                Executable gitExe = GetGitExe(cachedPath, environments);

                if (alreadyExists)
                {

                    Trace.WriteLine(String.Format("Using cached copy at location {0}", cachedPath));

                    // Get it into a clean state on the correct commit id
                    try
                    {
                        // If we don't have a specific commit id to go to, use origin/master
                        if (commitId == null)
                        {
                            commitId = "origin/master";
                        }

                        gitExe.Execute("reset --hard " + commitId);
                    }
                    catch (Exception e)
                    {
                        // Some repos like Drupal don't use a master branch (e.g. default branch is 7.x). In those cases,
                        // simply reset to the HEAD. That won't undo any test commits, but at least it does some cleanup.
                        if (e.Message.Contains("ambiguous argument 'origin/master'"))
                        {
                            gitExe.Execute("reset --hard HEAD");
                        }
                        else if (e.Message.Contains("unknown revision"))
                        {
                            // If that commit id doesn't exist, try fetching and doing the reset again
                            // The reason we don't initially fetch is to avoid the network hit when not necessary
                            gitExe.Execute("fetch origin");
                            gitExe.Execute("reset --hard " + commitId);
                        }
                        else
                        {
                            throw;
                        }
                    }
                    gitExe.Execute("clean -dxf");
                }
                else
                {
                    // Delete any leftover, ignoring errors
                    FileSystemHelpers.DeleteDirectorySafe(cachedPath);

                    Trace.WriteLine(String.Format("Could not find a cached copy at {0}. Cloning from source {1}.", cachedPath, source));
                    PathHelper.EnsureDirectory(cachedPath);
                    gitExe.Execute("clone \"{0}\" .", source);

                    // If we have a commit id, reset to it in case it's older than the latest on our clone
                    if (commitId != null)
                    {
                        gitExe.Execute("reset --hard " + commitId);
                    }
                }
            }
            return cachedPath;
        }

        public static GitDeploymentResult GitDeploy(string kuduServiceUrl, string localRepoPath, string remoteRepoUrl, string localBranchName, string remoteBranchName)
        {
            var deploymentManager = new RemoteDeploymentManager(kuduServiceUrl + "deployments"); 
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

        public static TestRepository CreateLocalRepository(string repositoryName)
        {
            string targetPath = Path.Combine(PathHelper.ZippedRepositoriesDir, repositoryName + ".zip");

            // Get the path to the repository
            string zippedPath = PathHelper.GetPath(targetPath);

            // Unzip it
            ZipUtils.Unzip(zippedPath, PathHelper.LocalRepositoriesDir);

            return new TestRepository(repositoryName);
        }

        public static string GetRepositoryPath(string repositoryPath)
        {
            if (Path.IsPathRooted(repositoryPath))
            {
                return repositoryPath;
            }

            return Path.Combine(PathHelper.LocalRepositoriesDir, repositoryPath);
        }

        private static string ResolveGitPath()
        {
            string programFiles = SystemEnvironment.GetFolderPath(SystemEnvironment.SpecialFolder.ProgramFilesX86);
            string path = Path.Combine(programFiles, "Git", "bin", "git.exe");

            if (!File.Exists(path))
            {
                throw new InvalidOperationException("Unable to locate git.exe");
            }

            return path;
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
            exe.SetTraceLevel(2);
            exe.SetHttpVerbose(true);
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
    }
}
