using System;
using System.Threading;

namespace Kudu.Core.Infrastructure
{
    internal class ProgressWriter : IDisposable
    {
        private Thread _progressThread;
        private bool _writingProgress;
        private bool _running;

        private DateTime _lastWriteTime;

        public void Start()
        {
            if (_progressThread == null)
            {
                // Set the last write time and initialize progress thread
                _lastWriteTime = DateTime.Now;
                _running = true;
                _progressThread = new Thread(UpdateWriterState);
                _progressThread.Start();
            }
        }

        public void WriteOutLine(string value)
        {
            OnBeforeWrite();

            Console.Out.WriteLine(value);
        }

        public void WriteErrorLine(string value)
        {
            OnBeforeWrite();

            Console.Error.WriteLine(value);
        }

        private void OnBeforeWrite()
        {
            _lastWriteTime = DateTime.Now;

            if (_writingProgress)
            {
                // Go back to not writing progress and print a new line before
                // we start to write new content
                _writingProgress = false;
                Console.WriteLine();
            }
        }

        private void UpdateWriterState()
        {
            // Keep the background thread running
            while (_running)
            {
                if (!_writingProgress)
                {
                    // If 5 seconds elapsed since the last write then switch into progress writing state
                    var elapsed = DateTime.Now - _lastWriteTime;

                    if (elapsed.TotalSeconds >= 5)
                    {
                        _writingProgress = true;
                    }
                    else
                    {
                        Thread.Sleep(500);
                    }
                }
                else
                {
                    // Write progress
                    Console.Write(".");

                    Thread.Sleep(1000);
                }
            }
        }

        public void Dispose()
        {
            // Set running to false
            _running = false;

            if (_progressThread != null)
            {
                // Wait for the thread to terminate (should always happen since we set _running to false)
                _progressThread.Join();
                _progressThread = null;
            }

            OnBeforeWrite();
        }
    }
}
