using System;

namespace Kudu.Core.Jobs
{
    public class ContinuousJobStatus : IJobStatus, IEquatable<ContinuousJobStatus>
    {
        public const string FileNamePrefix = "status_";

        public static readonly ContinuousJobStatus Initializing = new ContinuousJobStatus() { Status = JobStatus.Initializing };
        public static readonly ContinuousJobStatus Starting = new ContinuousJobStatus() { Status = JobStatus.Starting };
        public static readonly ContinuousJobStatus PendingRestart = new ContinuousJobStatus() { Status = JobStatus.PendingRestart };
        public static readonly ContinuousJobStatus Stopped = new ContinuousJobStatus() { Status = JobStatus.Stopped };
        public static readonly ContinuousJobStatus InactiveInstance = new ContinuousJobStatus() { Status = JobStatus.InactiveInstance };
        public static readonly ContinuousJobStatus Disabling = new ContinuousJobStatus() { Status = JobStatus.Disabling };
        public static readonly ContinuousJobStatus Stopping = new ContinuousJobStatus() { Status = JobStatus.Stopping };

        public string Status { get; set; }

        public bool Equals(ContinuousJobStatus other)
        {
            return other != null && String.Equals(Status, other.Status, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ContinuousJobStatus);
        }

        public override int GetHashCode()
        {
            return Status != null ? Status.ToUpperInvariant().GetHashCode() : 0;
        }
    }
}
