using System;
using System.Collections.Generic;
using System.IO;

namespace Kudu.Core.Infrastructure
{
    public class PathLinuxUtility : PathUtilityBase
    {
        internal override string ResolveGitPath()
        {
            return ResolveRelativePathToUsrBin("git", Resources.Error_FailedToLocateGit);
        }

        internal override string ResolveHgPath()
        {
            return ResolveRelativePathToUsrBin("hg", Resources.Error_FailedToLocateHg);
        }

        internal override string ResolveSSHPath()
        {
            return ResolveRelativePathToUsrBin("ssh", Resources.Error_FailedToLocateSsh);
        }

        internal override string ResolveBashPath()
        {
            return "/bin/bash";
        }

        internal override string ResolveNpmJsPath()
        {
            // This is passed as the NPM_JS_PATH envvar, which is used during node deployment
            // scripts as the default if a more appropriate version isn't found.
            // It needs to point to an appropriate default version of npm-cli.js, not npm itself,
            // as it will be run as a parameter to node (interpreted javascript) and not as a raw executable.
            // For that reason, it also needs to be the full path, as $PATH resolution will not happen on it.
            return ResolveRelativePathToUsrBin("npm-cli.js", "npm-cli.js");
        }

        internal override string ResolveMSBuildPath()
        {
            return ResolveRelativePathToUsrBin("xbuild", "xbuild");
        }

        internal override string ResolveNpmGlobalPrefix()
        {
            return "/usr/share/npm";
        }

        internal override List<string> ResolveNodeNpmPaths()
        {
            return new List<string>() { "/usr/bin" };
        }

        public override bool PathsEquals(string path1, string path2)
        {
            return String.Equals(CleanPath(path1), CleanPath(path2), StringComparison.Ordinal);
        }

        internal override string ResolveNpmToolsPath(string toolName)
        {
            return $"/usr/local/bin/{toolName}";
        }

        private static string ResolveRelativePathToUsrBin(string relativePath, string errorMessage)
        {
            string path = Path.Combine("/usr/bin", relativePath);
            if (!File.Exists(path))
            {
                throw new InvalidOperationException(errorMessage);
            }
            return path;
        }
    }
}
