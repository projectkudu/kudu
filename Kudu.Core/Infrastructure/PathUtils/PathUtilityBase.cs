using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SystemEnvironment = System.Environment;

namespace Kudu.Core.Infrastructure
{
    public abstract class PathUtilityBase
    {
        public virtual List<string> GetPathFolders(IEnvironment environment)
        {
            // Add the msbuild path and git path to the %PATH% so more tools are available
            var toolsPaths = new List<string> {
                environment.ScriptPath,
                Path.GetDirectoryName(ResolveMSBuildPath()),
                Path.GetDirectoryName(ResolveGitPath()),
                Path.GetDirectoryName(ResolveVsTestPath()),
                Path.GetDirectoryName(ResolveSQLCmdPath()),
                Path.GetDirectoryName(ResolveFSharpCPath())
            };

            toolsPaths.AddRange(ResolveNodeNpmPaths());
            toolsPaths.Add(ResolveNpmGlobalPrefix());

            toolsPaths.AddRange(new[]
            {
                ResolveBowerPath(),
                ResolveGruntPath(),
                ResolveGulpPath()
            }.Where(p => !String.IsNullOrEmpty(p)).Select(Path.GetDirectoryName));

            // Add /site/deployments/tools to the path to allow users to drop tools in there
            toolsPaths.Add(environment.DeploymentToolsPath);

            return toolsPaths;
        }

        internal abstract string ResolveGitPath();

        internal abstract string ResolveHgPath();

        internal abstract string ResolveSSHPath();

        internal abstract string ResolveBashPath();

        internal abstract string ResolveNpmJsPath();

        internal abstract string ResolveMSBuildPath();

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

        internal abstract bool PathsEquals(string path1, string path2);

        internal abstract string ResolveBowerPath();

        internal abstract string ResolveGulpPath();

        internal abstract string ResolveGruntPath();

        internal virtual string ResolveFSharpCPath()
        {
            string programFiles = SystemEnvironment.GetFolderPath(SystemEnvironment.SpecialFolder.ProgramFilesX86);
            return Path.Combine(programFiles, @"Microsoft SDKs", "F#", "3.1", "Framework", "v4.0", "Fsc.exe");
        }
    }
}
