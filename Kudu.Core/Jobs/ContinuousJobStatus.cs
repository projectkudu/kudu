namespace Kudu.Core.Jobs
{
    public class ContinuousJobStatus : IJobStatus
    {
        public static readonly ContinuousJobStatus Initializing = new ContinuousJobStatus() { Status = "Initializing" };
        public static readonly ContinuousJobStatus Starting = new ContinuousJobStatus() { Status = "Starting" };
        public static readonly ContinuousJobStatus Waiting = new ContinuousJobStatus() { Status = "Waiting" };
        public static readonly ContinuousJobStatus Stopped = new ContinuousJobStatus() { Status = "Stopped" };

        public string Status { get; set; }
    }
}
