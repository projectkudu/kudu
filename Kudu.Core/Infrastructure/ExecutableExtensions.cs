using System;
using System.Collections.Generic;
using System.Linq;
using Kudu.Contracts.Settings;
using System.IO;

namespace Kudu.Core.Infrastructure
{
    internal static class ExecutableExtensions
    {
        public static void PrependToPath(this Executable exe, IEnumerable<string> paths)
        {
            if (!paths.Any())
            {
                throw new ArgumentNullException("paths");
            }

            string pathEnv;
            exe.EnvironmentVariables.TryGetValue("PATH", out pathEnv);
            if (!String.IsNullOrEmpty(pathEnv))
            {
                paths = paths.Concat(new[] { pathEnv });
            }

            exe.EnvironmentVariables["PATH"] = String.Join(Path.PathSeparator.ToString(), paths);
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
