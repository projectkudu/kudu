using System;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Deployment
{
    internal class NpmExecutable : Executable
    {
        public NpmExecutable(string workingDirectory)
            : base(PathUtility.ResolveNpmPath(), workingDirectory)
        {
            Encoding = null;
        }

        public string Install(ITracer tracer, ConsoleWriter consoleWriter)
        {
            return Execute(tracer,
                           output =>
                           {
                               consoleWriter.WriteOutLine(output);
                               return true;
                           },
                           error =>
                           {
                               consoleWriter.WriteOutLine(error);
                               return true;
                           },
                           Console.OutputEncoding,
                           "install").Item1;
        }
    }
}
