using System;
using System.IO;
using System.Threading;

namespace Kudu.Core.Infrastructure
{
    /// <summary>
    /// The <see cref="ProgressWriter"/> class takes two <see cref="TextWriter"/> instances (output and error)
    /// and acts as a write-through of data. However, if nothing is written to these two writers for a given amount 
    /// of time then the <see cref="ProgressWriter"/> goes into "idle" mode which causes a sequence of "." to 
    /// be written until new data is written to the output or error.
    /// </summary>
    internal class ProgressWriter : IDisposable
    {
        private static readonly TimeSpan _defaultIdlingStart = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan _defaultIdlingDelay = TimeSpan.FromSeconds(1);

        private readonly Timer _timer;
        private readonly TextWriter _output;
        private readonly TextWriter _error;

        private TimeSpan _idlingStart;
        private TimeSpan _idlingDelay;

        private object _thisLock = new object();
        private bool _idling;
        private bool disposed;

        /// <summary>
        /// Creates a new <see cref="ProgessWriter"/> instance defaulting to Console.Out and Console.Error and with 
        /// default idling start and delay times.
        /// </summary>
        public ProgressWriter()
            : this(Console.Out, Console.Error)
        {
        }

        /// <summary>
        /// Creates a new <see cref="ProgessWriter"/> instance.
        /// </summary>
        /// <param name="output">The output writer. The idle character (".") is always written to this writer. Writer is not disposed.</param>
        /// <param name="error">The error writer. Writer is not disposed.</param>
        /// <param name="idlingStart">The amount of time until the writers are consider to be idling.</param>
        /// <param name="idlingDelay">The amount of time between each idle character (".") written while idling.</param>
        public ProgressWriter(TextWriter output, TextWriter error, TimeSpan? idlingStart = null, TimeSpan? idlingDelay = null)
        {
            if (output == null)
            {
                throw new ArgumentNullException("output");
            }
            if (error == null)
            {
                throw new ArgumentNullException("error");
            }
            _output = output;
            _error = error;

            _idlingStart = idlingStart != null ? idlingStart.Value : _defaultIdlingStart;
            _idlingDelay = idlingDelay != null ? idlingDelay.Value : _defaultIdlingDelay;

            // Set timer to fire first time we go idle
            _timer = new Timer(IdleWriter, null, _idlingStart, Timeout.InfiniteTimeSpan);
        }

        public void WriteOutLine(string value)
        {
            lock (_thisLock)
            {
                OnBeforeWrite();
                _output.WriteLine(value);
            }
        }

        public void WriteErrorLine(string value)
        {
            lock (_thisLock)
            {
                OnBeforeWrite();
                _error.WriteLine(value);
            }
        }

        private void OnBeforeWrite()
        {
            if (!disposed)
            {
                if (_idling)
                {
                    _idling = false;

                    // Go back to not writing progress and print a new line before we start to write new content
                    _output.WriteLine();
                }

                // Set next timer to fire when we would go idle
                _timer.Change(_idlingStart, Timeout.InfiniteTimeSpan);
            }
        }

        private void IdleWriter(object state)
        {
            lock (_thisLock)
            {
                if (!disposed)
                {
                    _idling = true;

                    // Write progress
                    _output.Write(".");

                    // Set next timer to fire when we write the next idle progress
                    _timer.Change(_idlingDelay, Timeout.InfiniteTimeSpan);
                }
            }
        }

        public void Dispose()
        {
            lock (_thisLock)
            {
                if (_idling)
                {
                    _output.WriteLine();
                }
                disposed = true;
            }

            if (_timer != null)
            {
                _timer.Dispose();
            }
        }
    }
}
