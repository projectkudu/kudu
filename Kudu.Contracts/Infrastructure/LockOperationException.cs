using System;

namespace Kudu.Contracts.Infrastructure
{
    public class LockOperationException : InvalidOperationException
    {
        public LockOperationException(string message)
            : base(message)
        {
        }
    }
}
