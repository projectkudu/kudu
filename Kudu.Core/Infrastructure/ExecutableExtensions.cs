using Kudu.Contracts.Settings;
using System;
using System.Collections.Generic;

namespace Kudu.Core.Infrastructure
{
    internal static class ExecutableExtensions
    {
        public static void AddToPath(this Executable exe, params string[] paths)
        {
            string pathEnv;
            exe.EnvironmentVariables.TryGetValue("PATH", out pathEnv);
            pathEnv = pathEnv ?? String.Empty;
            if (pathEnv.Length > 0 && !pathEnv.EndsWith(";", StringComparison.OrdinalIgnoreCase))
            {
                pathEnv += ";";
            }

            pathEnv += String.Join(";", paths);
            exe.EnvironmentVariables["PATH"] = pathEnv;
        }

        public static void AddDeploymentSettingsAsEnvironmentVariables(this Executable exe, IDeploymentSettingsManager deploymentSettingsManager)
        {
            IEnumerable<KeyValuePair<string, string>> deploymentSettings = deploymentSettingsManager.GetValues();
            foreach (var keyValuePair in deploymentSettings)
            {
                exe.EnvironmentVariables[keyValuePair.Key] = keyValuePair.Value;
            }
        }
    }
}
