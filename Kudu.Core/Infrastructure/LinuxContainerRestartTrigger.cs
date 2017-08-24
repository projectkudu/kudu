using System;
using System.Globalization;
using System.IO;
using Kudu.Core.Helpers;

namespace Kudu.Core.Infrastructure
{
    // Utility for touching the restart trigger file on Linux, which will restart the
    // site container.
    // Contents of the trigger file are irrelevant but this leaves a small explanation for
    // users who stumble on it.
    public static class LinuxContainerRestartTrigger
    {
        private static readonly string FILE_CONTENTS_FORMAT = String.Concat(
            "Modifying this file will trigger a restart of the app container.",
            System.Environment.NewLine, System.Environment.NewLine,
            "The last modification Kudu made to this file was at {0}, for the following reason: {1}.",
            System.Environment.NewLine);

        private static string restartTriggerPath;

        public static void Initialize(string siteRootPath)
        {
            restartTriggerPath = Path.Combine(siteRootPath, "config", "restartTrigger.txt");
        }

        public static void RequestContainerRestart(string reason)
        {
            if (OSDetector.IsOnWindows())
            {
                throw new NotSupportedException("RequestContainerRestart not supported on Windows");
            }

            if (restartTriggerPath == null)
            {
                throw new InvalidOperationException("LinuxContainerRestartTrigger not initialized");
            }

            FileSystemHelpers.CreateDirectory(Path.GetDirectoryName(restartTriggerPath));

            var fileContents = String.Format(
                FILE_CONTENTS_FORMAT,
                DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                reason);

            FileSystemHelpers.WriteAllText(restartTriggerPath, fileContents);
        }
    }
}
