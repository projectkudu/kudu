using System;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Deployment
{
    internal class NpmExecutable : Executable
    {
        public NpmExecutable(string workingDirectory, TimeSpan idleTimeout)
            : base(PathUtility.ResolveNpmPath(), workingDirectory, idleTimeout)
        {
            Encoding = null;
        }

        public string Install(ITracer tracer, ProgressWriter writer)
        {
            return RunCommandWithProgress(tracer, writer, "install --production");
        }

        internal string Rebuild(ITracer tracer, ProgressWriter writer)
        {
            return RunCommandWithProgress(tracer, writer, "rebuild");
        }

        private string RunCommandWithProgress(ITracer tracer, ProgressWriter writer, string command)
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
                           command).Item1;
        }
    }
}
