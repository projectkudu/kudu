using System;
using System.IO;
using SystemEnvironment = System.Environment;

namespace Kudu.Core.Infrastructure
{
    internal static class PathUtility
    {
        private const string ProgramFiles64bitKey = "ProgramW6432";
        private const string IISNodeExePath = "IISNODE_NODEPROCESSCOMMANDLINE";

        internal static string ResolveGitPath()
        {
            string programFiles = SystemEnvironment.GetFolderPath(SystemEnvironment.SpecialFolder.ProgramFilesX86);
            string path = Path.Combine(programFiles, "Git", "bin", "git.exe");

            if (!File.Exists(path))
            {
                throw new InvalidOperationException(Resources.Error_FailedToLocateGit);
            }

            return path;
        }

        internal static string ResolveHgPath()
        {
            string programFiles32 = SystemEnvironment.GetFolderPath(SystemEnvironment.SpecialFolder.ProgramFilesX86);
            string path = Path.Combine(programFiles32, "Mercurial", "hg.exe");

            if (!File.Exists(path))
            {
                string programFiles = SystemEnvironment.GetEnvironmentVariable(ProgramFiles64bitKey) ?? SystemEnvironment.GetFolderPath(SystemEnvironment.SpecialFolder.ProgramFiles);
                path = Path.Combine(programFiles, "Mercurial", "hg.exe");

                if (!File.Exists(path))
                {
                    throw new InvalidOperationException(Resources.Error_FailedToLocateHg);
                }
            }

            return path;
        }

        internal static string ResolveSSHPath()
        {
            string programFiles = SystemEnvironment.GetFolderPath(SystemEnvironment.SpecialFolder.ProgramFilesX86);
            string path = Path.Combine(programFiles, "Git", "bin", "ssh.exe");

            if (!File.Exists(path))
            {
                throw new InvalidOperationException(Resources.Error_FailedToLocateSsh);
            }

            return path;
        }

        internal static string ResolveNpmPath()
        {
            string programFiles = SystemEnvironment.GetFolderPath(SystemEnvironment.SpecialFolder.ProgramFilesX86);
            return Path.Combine(programFiles, "nodejs", "npm.cmd");
        }

        internal static string ResolveNpmJsPath()
        {
            string programFiles = SystemEnvironment.GetFolderPath(SystemEnvironment.SpecialFolder.ProgramFilesX86);
            return Path.Combine(programFiles, "nodejs", "node_modules", "npm", "bin", "npm-cli.js");
        }

        internal static string ResolveMSBuildPath()
        {
            string windir = SystemEnvironment.GetFolderPath(SystemEnvironment.SpecialFolder.Windows);
            return Path.Combine(windir, @"Microsoft.NET", "Framework", "v4.0.30319", "MSBuild.exe");
        }

        internal static string ResolveVsTestPath()
        {
            string programFiles = SystemEnvironment.GetFolderPath(SystemEnvironment.SpecialFolder.ProgramFilesX86);
            return Path.Combine(programFiles, "Microsoft Visual Studio 11.0", "Common7", "IDE", "CommonExtensions", "Microsoft", "TestWindow", "vstest.console.exe");
        }

        /// <summary>
        /// Returns the path in the environment variable IISNODE_NodeProcessCommandLine.
        /// </summary>
        /// <remarks>
        /// IISNODE_NodeProcessCommandLine is used by IISNode if it is unable to determine the version of node from 
        /// packages.json \ iisnode.yml files. 
        /// </remarks>
        internal static string ResolveIISNodeExePath()
        {
            return SystemEnvironment.GetEnvironmentVariable(IISNodeExePath);
        }

        internal static string CleanPath(string path)
        {
            if (path == null)
            {
                return null;
            }

            return Path.GetFullPath(path.Trim()).TrimEnd(Path.DirectorySeparatorChar);
        }

        internal static bool PathsEquals(string path1, string path2)
        {
            if (path1 == null)
            {
                return path2 == null;
            }

            return String.Equals(CleanPath(path1), CleanPath(path2), StringComparison.OrdinalIgnoreCase);
        }
    }
}
