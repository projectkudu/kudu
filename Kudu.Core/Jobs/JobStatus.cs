namespace Kudu.Core.Jobs
{
    public static class JobStatus
    {
        public const string Initializing = "Initializing";
        public const string Running = "Running";
        public const string Stopped = "Stopped";
        public const string Starting = "Starting";
        public const string PendingRestart = "PendingRestart";
        public const string InactiveInstance = "InactiveInstance";
        public const string Failed = "Failed";
        public const string Aborted = "Aborted";
        public const string Disabling = "Disabling";
        public const string Stopping = "Stopping";
    }
}
