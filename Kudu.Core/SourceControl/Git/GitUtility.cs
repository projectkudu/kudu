using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SystemEnvironment = System.Environment;
using System.IO;

namespace Kudu.Core.SourceControl.Git {
    static class GitUtility {
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
