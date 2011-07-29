using System;
using System.IO;
using SystemEnvironment = System.Environment;

namespace Kudu.Core.SourceControl.Git {
    internal static class GitUtility {
        internal static string ResolveGitPath() {
            string programFiles = SystemEnvironment.GetFolderPath(SystemEnvironment.SpecialFolder.ProgramFilesX86);
            string path = Path.Combine(programFiles, "Git", "bin", "git.exe");

            if (!File.Exists(path)) {
                throw new InvalidOperationException("Unable to locate git.exe");
            }

            return path;
        }
    }
}
