using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SystemEnvironment = System.Environment;

namespace Kudu.Core.Infrastructure
{
    public class PathWindowsUtility : PathUtilityBase
    {
        private const string ProgramFiles64bitKey = "ProgramW6432";

        /// <summary>
        /// The version of node.exe that would be on PATH, when the user does not specify/specifies invalid node versions.
        /// </summary>
        private const string DefaultNodeVersion = "0.10.28";

        /// <summary>
        /// Maps to the version of NPM that shipped with the DefaultNodeVersion
        /// </summary>
        private const string DefaultNpmVersion = "1.4.9";

        internal override string ResolveGitPath()
        {
            string relativePath = Path.Combine("Git", "bin", "git.exe");
            return ResolveRelativePathToProgramFiles(relativePath, relativePath, Resources.Error_FailedToLocateGit);
        }

        internal override string ResolveHgPath()
        {
            string relativePath = Path.Combine("Mercurial", "hg.exe");
            return ResolveRelativePathToProgramFiles(relativePath, relativePath, Resources.Error_FailedToLocateHg);
        }

        internal override string ResolveSSHPath()
        {
            // version that before 2.5, ssh.exe has different path than in version 2.5
            string relativeX86Path = Path.Combine("Git", "bin", "ssh.exe");
            string programFiles = SystemEnvironment.GetFolderPath(SystemEnvironment.SpecialFolder.ProgramFilesX86);
            string path = Path.Combine(programFiles, relativeX86Path);
            if (File.Exists(path))
            {
                return path;
            }

            string relativePath = Path.Combine("Git", "usr", "bin", "ssh.exe");
            return ResolveRelativePathToProgramFiles(relativePath, relativePath, Resources.Error_FailedToLocateSsh);
        }

        internal override string ResolveBashPath()
        {
            string relativePath = Path.Combine("Git", "bin", "bash.exe");
            return ResolveRelativePathToProgramFiles(relativePath, relativePath, Resources.Error_FailedToLocateBash);
        }

        internal override string ResolveNpmJsPath()
        {
            string programFiles = SystemEnvironment.GetFolderPath(SystemEnvironment.SpecialFolder.ProgramFilesX86);
            string npmCliPath = Path.Combine("node_modules", "npm", "bin", "npm-cli.js");
            string npmVersion = ResolveNpmVersion();

            // 1. Attempt to look for the file under the S24 updated path that looks like
            // "C:\Program Files (x86)\npm\1.3.8\node_modules\npm\bin\npm-cli.js"
            string npmPath = Path.Combine(programFiles, "npm", npmVersion, npmCliPath);
            if (File.Exists(npmPath))
            {
                return npmPath;
            }

            // 2. Attempt to look for the file under the pre-S24 npm path
            // "C:\Program Files (x86)\npm\1.3.8\bin\npm-cli.js"
            npmPath = Path.Combine(programFiles, "npm", npmVersion, "bin", "npm-cli.js");
            if (File.Exists(npmPath))
            {
                return npmPath;
            }

            // 3. Use the default npm path from the NodeJS installation
            // "C:\Program Files (x86)\nodejs\node_modules\npm\bin\npm-cli.js"
            return Path.Combine(programFiles, "nodejs", npmCliPath);
        }

        internal override string ResolveMSBuildPath()
        {
            string programFiles = SystemEnvironment.GetFolderPath(SystemEnvironment.SpecialFolder.ProgramFilesX86);
            return Path.Combine(programFiles, @"MSBuild", "14.0", "Bin", "MSBuild.exe");
        }

        internal override string ResolveVsTestPath()
        {
            string programFiles = SystemEnvironment.GetFolderPath(SystemEnvironment.SpecialFolder.ProgramFilesX86);
            return Path.Combine(programFiles, "Microsoft Visual Studio 11.0", "Common7", "IDE", "CommonExtensions", "Microsoft", "TestWindow", "vstest.console.exe");
        }

        internal override string ResolveSQLCmdPath()
        {
            string programFiles = SystemEnvironment.GetFolderPath(SystemEnvironment.SpecialFolder.ProgramFilesX86);
            return Path.Combine(programFiles, "Microsoft SQL Server", "110", "Tools", "Binn", "sqlcmd.exe");
        }

        internal override string ResolveNpmGlobalPrefix()
        {
            string appDataDirectory = SystemEnvironment.GetFolderPath(SystemEnvironment.SpecialFolder.ApplicationData);
            return Path.Combine(appDataDirectory, "npm");
        }

        internal override string ResolveVCTargetsPath()
        {
            string programFiles = SystemEnvironment.GetFolderPath(SystemEnvironment.SpecialFolder.ProgramFilesX86);
            //This path has to end with \ for the environment variable to work
            return Path.Combine(programFiles, "MSBuild", "Microsoft.Cpp", "v4.0", @"V140\");
        }

        internal override string ResolveVCInstallDirPath()
        {
            string programFiles = SystemEnvironment.GetFolderPath(SystemEnvironment.SpecialFolder.ProgramFilesX86);
            //This path has to end with \ for the environment variable to work
            return Path.Combine(programFiles, "Microsoft Visual Studio 14.0", @"VC\");
        }

        internal override List<string> ResolveNodeNpmPaths()
        {
            var paths = new List<string>();
            string programFiles = SystemEnvironment.GetFolderPath(SystemEnvironment.SpecialFolder.ProgramFilesX86);
            bool fromAppSetting;
            string nodeVersion = ResolveNodeVersion(out fromAppSetting);

            // Only set node.exe path when WEBSITE_NODE_DEFAULT_VERSION is not set in app setting and the path is invalid.
            if (!fromAppSetting)
            {
                string nodePath = Path.Combine(programFiles, "nodejs", nodeVersion);
                if (FileSystemHelpers.FileExists(Path.Combine(nodePath, "node.exe")))
                {
                    paths.Add(nodePath);
                }
            }

            // Only set npm.cmd path when npm.cmd can be found
            string npmVersion = ResolveNpmVersion(nodeVersion);
            string npmPath = Path.Combine(programFiles, "npm", npmVersion);
            if (FileSystemHelpers.FileExists(Path.Combine(npmPath, "npm.cmd")))
            {
                paths.Add(npmPath);
            }

            return paths;
        }

        internal override bool PathsEquals(string path1, string path2)
        {
            if (path1 == null)
            {
                return path2 == null;
            }

            return String.Equals(CleanPath(path1), CleanPath(path2), StringComparison.OrdinalIgnoreCase);
        }

        internal override string ResolveBowerPath()
        {
            return ResolveNpmToolsPath("bower");
        }

        internal override string ResolveGulpPath()
        {
            return ResolveNpmToolsPath("gulp");
        }

        internal override string ResolveGruntPath()
        {
            return ResolveNpmToolsPath("grunt");
        }

        private static string ResolveNodeVersion()
        {
            bool fromAppSetting;
            return ResolveNodeVersion(out fromAppSetting);
        }

        private static string ResolveNodeVersion(out bool fromAppSetting)
        {
            // We can't use ConfigurationManager.AppSettings[] here because during git push this runs in kudu.exe context
            // which doesn't have access to Azure ConfigurationManager.AppSettings[] of the site
            string appSettingNodeVersion = SystemEnvironment.GetEnvironmentVariable("APPSETTING_WEBSITE_NODE_DEFAULT_VERSION");

            if (IsNodeVersionInstalled(appSettingNodeVersion))
            {
                fromAppSetting = true;
                return appSettingNodeVersion;
            }
            else
            {
                fromAppSetting = false;
                return DefaultNodeVersion;
            }
        }

        private static string ResolveNpmVersion()
        {
            return ResolveNpmVersion(ResolveNodeVersion());
        }

        private static string ResolveNpmVersion(string nodeVersion)
        {
            string programFiles = SystemEnvironment.GetFolderPath(SystemEnvironment.SpecialFolder.ProgramFilesX86);
            string appSettingNpmVersion = SystemEnvironment.GetEnvironmentVariable("WEBSITE_NPM_DEFAULT_VERSION");

            if (!string.IsNullOrEmpty(appSettingNpmVersion))
            {
                return appSettingNpmVersion;
            }
            else if (nodeVersion.Equals("4.1.2", StringComparison.OrdinalIgnoreCase))
            {
                // This case is only to work around node version 4.1.2 with npm 2.x failing to publish ASP.NET 5 apps due to long path issues.
                return "3.3.6";
            }
            else
            {
                string npmTxtPath = Path.Combine(programFiles, "nodejs", nodeVersion, "npm.txt");

                return FileSystemHelpers.FileExists(npmTxtPath) ? FileSystemHelpers.ReadAllTextFromFile(npmTxtPath).Trim() : DefaultNpmVersion;
            }
        }

        private static bool IsNodeVersionInstalled(string version)
        {
            string programFiles = SystemEnvironment.GetFolderPath(SystemEnvironment.SpecialFolder.ProgramFilesX86);
            return (!String.IsNullOrEmpty(version)) &&
                   FileSystemHelpers.FileExists(Path.Combine(programFiles, "nodejs", version, "node.exe"));
        }

        private static string ResolveNpmToolsPath(string toolName)
        {
            // If there is a TOOLNAME_PATH specified, then use that.
            // Otherwise use the pre-installed one
            var toolPath = SystemEnvironment.GetEnvironmentVariable(String.Format("APPSETTING_{0}_PATH", toolName));
            if (String.IsNullOrEmpty(toolPath))
            {
                var programFiles = SystemEnvironment.GetFolderPath(SystemEnvironment.SpecialFolder.ProgramFilesX86);
                var toolRootPath = Path.Combine(programFiles, toolName);
                if (Directory.Exists(toolRootPath))
                {
                    // If there is a TOOLNAME_VERSION defined, use that.
                    // Otherwise use the latest one.
                    var userVersion = SystemEnvironment.GetEnvironmentVariable(String.Format("APPSETTING_{0}_VERSION", toolName));

                    toolPath = String.IsNullOrEmpty(userVersion)
                        ? Directory.GetDirectories(toolRootPath).OrderByDescending(p => p, StringComparer.OrdinalIgnoreCase).FirstOrDefault()
                        : Path.Combine(toolRootPath, userVersion);
                }
            }

            return String.IsNullOrEmpty(toolPath)
                ? String.Empty
                : Path.Combine(toolPath, String.Format("{0}.cmd", toolName));
        }

        private static string ResolveRelativePathToProgramFiles(string relativeX86Path, string relativeX64Path, string errorMessge)
        {
            string programFiles = SystemEnvironment.GetFolderPath(SystemEnvironment.SpecialFolder.ProgramFilesX86);
            string path = Path.Combine(programFiles, relativeX86Path);
            if (!File.Exists(path))
            {
                programFiles = SystemEnvironment.GetEnvironmentVariable(ProgramFiles64bitKey) ?? SystemEnvironment.GetFolderPath(SystemEnvironment.SpecialFolder.ProgramFiles);
                path = Path.Combine(programFiles, relativeX64Path);
            }

            if (!File.Exists(path))
            {
                throw new InvalidOperationException(errorMessge);
            }

            return path;
        }
    }
}
