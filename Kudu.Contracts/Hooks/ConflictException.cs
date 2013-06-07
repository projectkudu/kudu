using System;

namespace Kudu.Core.Hooks
{
    public class ConflictException : InvalidOperationException
    {
        public ConflictException()
            : base()
        {
        }
    }
}
