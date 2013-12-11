using System;
using System.Diagnostics;
using System.IO;
using Kudu.Contracts.Tracing;

namespace Kudu.Core.Infrastructure
{
    public class ProcessWrapper : IProcess
    {
        private readonly Process _process;

        public ProcessWrapper(Process process)
        {
            _process = process;
            Name = Path.GetFileName(process.StartInfo.FileName);
        }

        public string Name { get; private set; }

        public string Arguments
        {
            get { return _process.StartInfo.Arguments; }
        }

        public bool Start()
        {
            return _process.Start();
        }

        public void WaitUntilEOF()
        {
            _process.WaitForExit(-1);
        }

        public bool WaitForExit(TimeSpan timeSpan)
        {
            return _process.WaitForExit((int)timeSpan.TotalMilliseconds);
        }

        public void Kill(ITracer tracer)
        {
            _process.Kill(includesChildren: true, tracer: tracer);
        }

        public TimeSpan GetTotalProcessorTime(ITracer tracer)
        {
            return _process.GetTotalProcessorTime(tracer);
        }

        public int Id
        {
            get { return _process.Id; }
        }

        public bool HasExited
        {
            get { return _process.HasExited; }
        }

        public int ExitCode
        {
            get { return _process.ExitCode; }
        }

        public StreamReader StandardError
        {
            get { return _process.StandardError; }
        }

        public StreamWriter StandardInput
        {
            get { return _process.StandardInput; }
        }

        public StreamReader StandardOutput
        {
            get { return _process.StandardOutput; }
        }

        public void Kill(bool includesChildren, ITracer tracer)
        {
            _process.Kill(includesChildren, tracer);
        }
    }
}
