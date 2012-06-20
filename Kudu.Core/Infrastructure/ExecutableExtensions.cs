using System;

namespace Kudu.Core.Infrastructure
{
    internal static class ExecutableExtensions
    {
        public static void AddToPath(this Executable exe, params string[] paths)
        {
            string pathEnv;
            exe.EnvironmentVariables.TryGetValue("PATH", out pathEnv);
            pathEnv = pathEnv ?? String.Empty;
            if (!pathEnv.EndsWith(";"))
            {
                pathEnv += ";";
            }

            pathEnv += String.Join(";", paths);
            exe.EnvironmentVariables["PATH"] = pathEnv;
        }
    }
}
