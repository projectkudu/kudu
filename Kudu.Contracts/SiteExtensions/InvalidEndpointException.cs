using System;

namespace Kudu.Contracts.SiteExtensions
{
    public class InvalidEndpointException : Exception
    {
        public InvalidEndpointException(string message)
            : base(message)
        { }

        public InvalidEndpointException(string message, Exception innerException)
            : base(message, innerException)
        { }
    }
}
