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

        public string Install(ITracer tracer, ProgressWriter writer)
        {
            return Execute(tracer,
                           output =>
                           {
                               writer.WriteOutLine(output);
                               return true;
                           },
                           error =>
                           {
                               writer.WriteOutLine(error);
                               return true;
                           },
                           Console.OutputEncoding,
                           "install").Item1;
        }
    }
}
