using System;
using Kudu.Contracts.Tracing;

namespace Kudu.Core.Infrastructure
{
    public interface IProcess
    {
        /// <summary>
        /// The file name of the executable
        /// </summary>
        string Name { get; }

        /// <summary>
        /// The arguments passed in to the executable.
        /// </summary>
        string Arguments { get; }

        /// <summary>
        /// Equivalent to Process.WaitForExit(-1)
        /// </summary>
        /// <remarks>
        /// Once we are here, the process has terminated.  This extra WaitForExit with -1 timeout
        /// will ensure in-memory Output buffer is flushed, from reflection, this.output.WaitUtilEOF().  
        /// If we don't do this, the leftover output will write concurrently to the logger 
        /// with the main thread corrupting the log xml.  
        /// </remarks>
        void WaitUntilEOF();

        /// <summary>
        /// Waits for the at most specified duration for the process to exit.
        /// </summary>
        /// <param name="timeSpan">The maximum duration to wait for.</param>
        /// <returns></returns>
        bool WaitForExit(TimeSpan timeSpan);

        /// <summary>
        /// Kills the process and all child processes spawned by it.
        /// </summary>
        void Kill(ITracer tracer);

        /// <summary>
        /// Gets the TotalProcessTime for the process tree in milliseconds
        /// </summary>
        /// <see cref="http://msdn.microsoft.com/en-us/library/system.diagnostics.process.totalprocessortime.aspx"/>
        TimeSpan GetTotalProcessorTime(ITracer tracer);
    }
}
