using System;
using System.IO;
using Kudu.Core.Infrastructure;
using Kudu.Core.SourceControl.Git;
using Kudu.Web.Infrastructure;
using SystemEnvironment = System.Environment;


namespace Kudu.FunctionalTests.Infrastructure
{
    public static class Git {
        
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
            gitExe.Execute("commit -m \"{0}\"", message);
        }

        public static string CreateLocalRepository(string repositoryName)
        {
            // Get the path to the repository
            string zippedPath = Path.Combine(PathHelper.ZippedRepositoriesDir, repositoryName + ".zip");

            // Unzip it
            Utils.Unzip(zippedPath, PathHelper.LocalRepositoriesDir);

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
            string repositoryPath = Path.Combine(PathHelper.LocalRepositoriesDir, repositoryName);
            return new Executable(ResolveGitPath(), repositoryPath);
        }

    }
}
