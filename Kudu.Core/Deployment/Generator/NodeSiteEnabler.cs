using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;
using System;
using System.IO;
using System.IO.Abstractions;

namespace Kudu.Core.Deployment.Generator
{
    public class NodeSiteEnabler
    {
        private static readonly string[] NodeStartFiles = new[] { "server.js", "app.js" };

        public static bool LooksLikeNode(IFileSystem fileSystem, string siteFolder)
        {
            // Check if any of the known start pages exist
            foreach (var nodeDetectionFile in NodeStartFiles)
            {
                string fullPath = Path.Combine(siteFolder, nodeDetectionFile);
                if (fileSystem.File.Exists(fullPath))
                {
                    return true;
                }
            }

            return false;
        }

        public static string SelectNodeVersion(IFileSystem fileSystem, string scriptPath, string sourcePath, ITracer tracer)
        {
            // The node.js version selection logic is implemented in selectNodeVersion.js. 

            // run with default node.js version which is on the path
            Executable executor = new Executable("node.exe", String.Empty);
            try
            {
                return executor.ExecuteWithConsoleOutput(
                    tracer,
                    "\"{0}\\selectNodeVersion.js\" \"{1}\" \"{1}\"",
                    scriptPath,
                    sourcePath).Item1;
            }
            catch (Exception e)
            {
                throw new InvalidOperationException(Resources.Error_UnableToSelectNodeVersion, e);
            }
        }
    }
}
