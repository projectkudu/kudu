using System;
using System.Globalization;
using System.IO;
using Kudu.Core.Helpers;

namespace Kudu.Core.Infrastructure
{
    // Utility for touching the restart trigger file on Linux and Windows Containers, which will restart the
    // site container.
    // Contents of the trigger file are irrelevant but this leaves a small explanation for
    // users who stumble on it.
    public static class DockerContainerRestartTrigger
    {
        private const string CONFIG_DIR_NAME = "config";
        private const string TRIGGER_FILENAME = "restartTrigger.txt";

        private static readonly string FILE_CONTENTS_FORMAT = String.Concat(
            "Modifying this file will trigger a restart of the app container.",
            System.Environment.NewLine, System.Environment.NewLine,
            "The last modification Kudu made to this file was at {0}, for the following reason: {1}.",
            System.Environment.NewLine);

        public static string GetRestartFilePath(IEnvironment environment)
        {
            return Path.Combine(environment.SiteRootPath, CONFIG_DIR_NAME, TRIGGER_FILENAME);
        }

        public static void RequestContainerRestart(IEnvironment environment, string reason)
        {
            if (OSDetector.IsOnWindows() && !EnvironmentHelper.IsWindowsContainers())
            {
                throw new NotSupportedException("RequestContainerRestart is only supported on Linux and Windows Containers");
            }

            var restartTriggerPath = GetRestartFilePath(environment);

            FileSystemHelpers.CreateDirectory(Path.GetDirectoryName(restartTriggerPath));

            var fileContents = String.Format(
                FILE_CONTENTS_FORMAT,
                DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                reason);

            FileSystemHelpers.WriteAllText(restartTriggerPath, fileContents);
        }
    }
}
