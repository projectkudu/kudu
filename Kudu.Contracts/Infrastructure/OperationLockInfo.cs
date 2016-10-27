using System;
using System.Threading.Tasks;

namespace Kudu.Contracts.Infrastructure
{
    public class OperationLockInfo
    {
        public OperationLockInfo()
        {
            AcquiredDateTime = DateTime.UtcNow.ToString("o");
        }

        public string OperationName { get; set; }
        public string AcquiredDateTime { get; set; }
        public string StackTrace { get; set; }
        public string InstanceId { get; set; }
    }
}
