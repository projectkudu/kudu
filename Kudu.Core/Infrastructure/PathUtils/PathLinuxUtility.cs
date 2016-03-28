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
            return ResolveRelativePathToUsrBin("npm", "npm");
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

        internal override bool PathsEquals(string path1, string path2)
        {
            return String.Equals(CleanPath(path1), CleanPath(path2), StringComparison.Ordinal);
        }

        internal override string ResolveBowerPath()
        {
            return "/usr/local/bin/bower";
        }

        internal override string ResolveGulpPath()
        {
            return "/usr/local/bin/gulp";
        }

        internal override string ResolveGruntPath()
        {
            return "/usr/local/bin/grunt";
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
