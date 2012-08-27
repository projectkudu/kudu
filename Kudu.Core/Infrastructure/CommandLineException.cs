using System;
using System.Runtime.Serialization;

namespace Kudu.Core.Infrastructure
{
    [Serializable]
    public class CommandLineException : Exception
    {
        public CommandLineException() { }

        public CommandLineException(string message)
            : base(message)
        {
        }

        public CommandLineException(string message, Exception inner)
            : base(message, inner)
        {
        }

        protected CommandLineException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        public int ExitCode { get; set; }
        public string Output { get; set; }
        public string Error { get; set; }
    }
}
