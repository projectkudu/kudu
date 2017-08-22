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
        // TODO look up root instead of hard-coding here
        private const string RESTART_TRIGGER_PATH = "/home/site/config/restartTrigger.txt";

        private static readonly string FILE_CONTENTS_FORMAT = String.Concat(
            "Modifying this file will trigger a restart of the app container.",
            System.Environment.NewLine, System.Environment.NewLine,
            "The last modification Kudu made to this file was at {0}, for the following reason: {1}.",
            System.Environment.NewLine);

        public static void RequestContainerRestart(string reason)
        {
            if (OSDetector.IsOnWindows())
            {
                throw new NotSupportedException("RequestContainerRestart not supported on Windows");
            }

            FileSystemHelpers.CreateDirectory(Path.GetDirectoryName(RESTART_TRIGGER_PATH));

            var fileContents = String.Format(
                FILE_CONTENTS_FORMAT,
                DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                reason);

            FileSystemHelpers.WriteAllText(RESTART_TRIGGER_PATH, fileContents);
        }
    }
}
