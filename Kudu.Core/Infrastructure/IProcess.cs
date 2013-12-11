using System;
using System.IO;
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
        /// Start the process
        /// </summary>
        bool Start();

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

        /// <summary>
        /// Gets the process Id
        /// </summary>
        int Id { get; }

        /// <summary>
        /// Gets whether the process has exited
        /// </summary>
        bool HasExited { get; }

        /// <summary>
        /// Gets the ExitCode for the process
        /// </summary>
        int ExitCode { get; }

        /// <summary>
        /// Gets the StandardError stream for the process
        /// </summary>
        StreamReader StandardError { get; }

        /// <summary>
        /// Gets the StandardInput stream for the process
        /// </summary>
        StreamWriter StandardInput { get; }

        /// <summary>
        /// Gets the StandardOutput stream for the process
        /// </summary>
        StreamReader StandardOutput { get; }
    }
}
