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

        public override string ToString()
        {
            return String.Format("ExitCode: {0}, Output: {1}, Error: {2}, {3}", 
                this.ExitCode,
                this.Output,
                this.Error,
                base.ToString());
        }
    }
}
