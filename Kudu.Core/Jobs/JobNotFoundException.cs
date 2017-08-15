using System;

namespace Kudu.Core.Jobs
{
    public class JobNotFoundException : InvalidOperationException
    {
        public JobNotFoundException(string message)
            : base(message)
        {
        }
    }
}