using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SystemEnvironment = System.Environment;

namespace Kudu.Core.Infrastructure
{
    public abstract class PathUtilityBase
    {
        static readonly string[] _npmTools = new string[]
        {
            "bower",
            "grunt",
            "gulp",
            "funcpack",
        };

        // Return a list of folders that need to be on the %PATH%
        public virtual List<string> GetPathFolders(IEnvironment environment)
        {
            // Add the msbuild path and git path to the %PATH% so more tools are available
            var toolsPaths = new List<string> {
                environment.DeploymentToolsPath, // Add /site/deployments/tools to the path to allow users to drop tools in there
                environment.ScriptPath,
                Path.GetDirectoryName(ResolveMSBuildPath()),
                Path.GetDirectoryName(ResolveGitPath()),
                Path.GetDirectoryName(ResolveVsTestPath()),
                Path.GetDirectoryName(ResolveSQLCmdPath()),
                Path.GetDirectoryName(ResolveFSharpCPath())
            };

            toolsPaths.AddRange(ResolveGitToolPaths());
            toolsPaths.AddRange(ResolveNodeNpmPaths());
            toolsPaths.Add(ResolveNpmGlobalPrefix());

            toolsPaths.AddRange(_npmTools.Select(ResolveNpmToolsPath)
                .Where(p => !String.IsNullOrEmpty(p)).Select(Path.GetDirectoryName));

            return toolsPaths;
        }

        internal abstract string ResolveGitPath();

        internal virtual string[] ResolveGitToolPaths()
        {
            return new string[] { };
        }

        internal abstract string ResolveHgPath();

        internal abstract string ResolveSSHPath();

        internal abstract string ResolveBashPath();

        internal abstract string ResolveNpmJsPath();

        internal abstract string ResolveMSBuildPath();

        internal virtual string ResolveMSBuild15Dir()
        {
            return null;
        }

        internal virtual string ResolveVsTestPath()
        {
            return null;
        }

        internal virtual string ResolveSQLCmdPath()
        {
            return null;
        }

        internal abstract string ResolveNpmGlobalPrefix();

        internal virtual string ResolveVCTargetsPath()
        {
            return null;
        }

        internal virtual string ResolveVCInstallDirPath()
        {
            return null;
        }

        internal abstract List<string> ResolveNodeNpmPaths();

        internal virtual string CleanPath(string path)
        {
            if (path == null)
            {
                return null;
            }

            return Path.GetFullPath(path.Trim()).TrimEnd(Path.DirectorySeparatorChar);
        }

        public abstract bool PathsEquals(string path1, string path2);

        internal abstract string ResolveNpmToolsPath(string toolName);

        internal virtual string ResolveFSharpCPath()
        {
            string programFiles = SystemEnvironment.GetFolderPath(SystemEnvironment.SpecialFolder.ProgramFilesX86);
            return Path.Combine(programFiles, @"Microsoft SDKs", "F#", "3.1", "Framework", "v4.0", "Fsc.exe");
        }
    }
}
