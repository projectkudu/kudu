using System;
using System.IO;
using SystemEnvironment = System.Environment;

namespace Kudu.Core.Infrastructure
{
    internal static class PathUtility
    {
        private const string ProgramFiles64bitKey = "ProgramW6432";
        /// <summary>
        /// The version of Node.exe that we'd use for running KuduScript and selectNodeVersion.js
        /// </summary>
        private const string DefaultNodeVersion = "0.10.5";

        /// <summary>
        /// Maps to the version of NPM that shipped with the DefaultNodeVersion
        /// </summary>
        private const string DefaultNpmVersion = "1.2.18";


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

        internal static string ResolveNpmJsPath()
        {
            string programFiles = SystemEnvironment.GetFolderPath(SystemEnvironment.SpecialFolder.ProgramFilesX86);
            string npmCliPath = Path.Combine("node_modules", "npm", "bin", "npm-cli.js");

            // 1. Attempt to look for the file under the S24 updated path that looks like 
            // "C:\Program Files (x86)\npm\1.3.8\node_modules\npm\bin\npm-cli.js"
            string npmPath = Path.Combine(programFiles, "npm", DefaultNpmVersion, npmCliPath);
            if (File.Exists(npmPath))
            {
                return npmPath;
            }

            // 2. Attempt to look for the file under the pre-S24 npm path
            // "C:\Program Files (x86)\npm\1.3.8\bin\npm-cli.js"
            npmPath = Path.Combine(programFiles, "npm", DefaultNpmVersion, "bin", "npm-cli.js");
            if (File.Exists(npmPath))
            {
                return npmPath;
            }

            // 3. Use the default npm path from the NodeJS installation
            // "C:\Program Files (x86)\nodejs\node_modules\npm\bin\npm-cli.js"
            return Path.Combine(programFiles, "nodejs", npmCliPath);
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
        /// Returns the path to the version of node.exe that is used for KuduScript generation and select node version
        /// </summary>
        /// <returns>
        /// The path to NodeJS version 0.10.5 if available, null otherwise.
        /// </remarks>
        internal static string ResolveNodePath()
        {
            string programFiles = SystemEnvironment.GetFolderPath(SystemEnvironment.SpecialFolder.ProgramFilesX86);

            string nodePath = Path.Combine(programFiles, "nodejs", "0.10.5", "node.exe");
            return File.Exists(nodePath) ? nodePath : null;
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
