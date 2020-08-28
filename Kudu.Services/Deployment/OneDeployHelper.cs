using Kudu.Contracts.Deployment;
using Kudu.Core;
using Kudu.Core.Deployment;
using Kudu.Core.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Kudu.Services.Deployment
{
    static class OneDeployHelper
    {
        // Stacks supported by OneDeploy 
        public const string Tomcat = "TOMCAT";
        public const string JavaSE = "JAVA";
        public const string JBossEap = "JBOSSEAP";

        private const string StackEnvVarName = "WEBSITE_STACK";

        // All paths are relative to HOME directory
        private const string ScriptsDirectoryRelativePath = "site/scripts";
        private const string LibsDirectoryRelativePath = "site/libs";

        public static bool IsLegacyWarPathValid(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            var segments = path.Split('/');

            return segments.Length == 2 && path.StartsWith("webapps/", StringComparison.Ordinal) & !string.IsNullOrWhiteSpace(segments[1]);
        }

        public static bool EnsureValidStack(string expectedStack, bool ignoreStack, out string error)
        {
            var websiteStack = GetWebsiteStack();

            if (ignoreStack || string.Equals(websiteStack, expectedStack, StringComparison.OrdinalIgnoreCase))
            {
                error = null;
                return true;
            }

            error = $"WAR files cannot be deployed to stack='{websiteStack}'. Expected stack='{expectedStack}'";
            return false;
        }

        public static bool EnsureValidPath(ArtifactType artifactType, string path, out string error)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                error = $"Path must be defined for type='{artifactType}'";
                return false;
            }

            error = null;
            return true;
        }

        public static string GetWebsiteStack()
        {
            return System.Environment.GetEnvironmentVariable(StackEnvVarName);
        }

        public static string GetLibsDirectoryAbsolutePath(IEnvironment environment)
        {
            return Path.Combine(environment.RootPath, LibsDirectoryRelativePath);
        }

        public static string GetScriptsDirectoryAbsolutePath(IEnvironment environment)
        {
            return Path.Combine(environment.RootPath, ScriptsDirectoryRelativePath);
        }

        public static string GetStartupFileName()
        {
            return OSDetector.IsOnWindows() ? "startup.cmd" : "startup.sh";
        }

        public static void SetTargetSubDirectoyAndFileNameFromPath(DeploymentInfoBase deploymentInfo, string relativeFilePath)
        {
            // Extract directory path and file name from relativeFilePath
            // Example: path=a/b/c.jar => TargetDirectoryName=a/b and TargetFileName=c.jar
            // Example: path=c.jar => TargetDirectoryName=null and TargetFileName=c.jar
            // Example: path=/c.jar => TargetDirectoryName="" and TargetFileName=c.jar
            // Example: path=null => TargetDirectoryName=null and TargetFileName=null
            deploymentInfo.TargetFileName = Path.GetFileName(relativeFilePath);
            deploymentInfo.TargetSubDirectoryRelativePath = Path.GetDirectoryName(relativeFilePath);
        }
    }
}
