using System;
using System.IO;
using Kudu.Core.Infrastructure;

namespace Kudu.TestHarness
{
    public static class Npm
    {
        public static void Install(string packageToInstall, string installToPath)
        {
            using (new LatencyLogger("npm install " + packageToInstall + " to " + installToPath))
            {
                string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                string npmPath = Path.Combine(programFiles, "nodejs", "npm.cmd");
                var exe = new Executable(npmPath, installToPath, idleTimeout: TimeSpan.FromSeconds(3600));
                var result = exe.Execute("install {0} .", packageToInstall);

                TestTracer.Trace("  stdout: {0}", result.Item1);
                TestTracer.Trace("  stderr: {0}", result.Item2);
            }
        }

        public static void InstallWithRetry(string packageToInstall, string installToPath)
        {
            OperationManager.Attempt(() => Install(packageToInstall, installToPath));
        }
    }
}
