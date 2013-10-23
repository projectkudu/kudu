using System;
using System.Globalization;

namespace Kudu.Core.Infrastructure
{
    public class CommandLineException : Exception
    {
        public CommandLineException(string executablePath, string arguments, string message)
            : base(String.Format(CultureInfo.InvariantCulture, "{0}{1}{2} {3}", message, System.Environment.NewLine, executablePath, arguments))
        {
        }

        public int ExitCode { get; set; }
        public string Output { get; set; }
        public string Error { get; set; }

        public override string ToString()
        {
            return String.Format(CultureInfo.InvariantCulture, "ExitCode: {0}, Output: {1}, Error: {2}, {3}", 
                this.ExitCode,
                this.Output,
                this.Error,
                base.ToString());
        }
    }
}
