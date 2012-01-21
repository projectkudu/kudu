using System;
using System.Diagnostics;
using System.IO;
using Kudu.Core.Infrastructure;
using Kudu.Core.SourceControl.Git;
using Kudu.Web.Infrastructure;
using SystemEnvironment = System.Environment;


namespace Kudu.FunctionalTests.Infrastructure
{
    public static class Git
    {

        public static void Push(string repositoryName, string url, string branchName = "master")
        {
            Executable gitExe = GetGitExe(repositoryName);
            gitExe.Execute("push {0} {1}", url, branchName);
        }

        public static void Revert(string repositoryName, string commit = "HEAD")
        {
            Executable gitExe = GetGitExe(repositoryName);
            gitExe.Execute("revert --no-edit \"{0}\"", commit);
        }

        public static void Reset(string repositoryName, string commit = "HEAD^")
        {
            Executable gitExe = GetGitExe(repositoryName);
            gitExe.Execute("reset --hard \"{0}\"", commit);
        }

        public static void Commit(string repositoryName, string message)
        {
            Executable gitExe = GetGitExe(repositoryName);
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

        public static void Add(string repositoryName, string path)
        {
            Executable gitExe = GetGitExe(repositoryName);
            gitExe.Execute("add \"{0}\"", path);
        }

        public static TestRepository Clone(string repositoryName, string source)
        {
            // Make sure the directory is empty
            string repositoryPath = GetRepositoryPath(repositoryName);
            FileSystemHelpers.DeleteDirectorySafe(repositoryPath);
            Executable gitExe = GetGitExe(repositoryName);

            gitExe.Execute("clone \"{0}\" .", source);

            return new TestRepository(repositoryName);
        }

        public static TestRepository CreateLocalRepository(string repositoryName)
        {
            // Get the path to the repository
            string zippedPath = Path.Combine(PathHelper.ZippedRepositoriesDir, repositoryName + ".zip");

            // Unzip it
            Utils.Unzip(zippedPath, PathHelper.LocalRepositoriesDir);

            return new TestRepository(repositoryName);
        }

        public static string GetRepositoryPath(string repositoryName)
        {
            return Path.Combine(PathHelper.LocalRepositoriesDir, repositoryName);
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

        private static Executable GetGitExe(string repositoryName)
        {
            string repositoryPath = GetRepositoryPath(repositoryName);

            FileSystemHelpers.EnsureDirectory(repositoryPath);

            return new Executable(ResolveGitPath(), repositoryPath);
        }

        public class TestRepository : IDisposable
        {
            private readonly string _physicalPath;
            private readonly GitExeRepository _repository;

            public TestRepository(string repositoryName)
            {
                _physicalPath = GetRepositoryPath(repositoryName);
                _repository = new GitExeRepository(_physicalPath);
            }


            public string CurrentId
            {
                get
                {
                    return _repository.CurrentId;
                }
            }

            public string PhysicalPath
            {
                get
                {
                    return _physicalPath;
                }
            }

            public void Dispose()
            {
                FileSystemHelpers.DeleteDirectorySafe(PhysicalPath);
            }
        }
    }
}
