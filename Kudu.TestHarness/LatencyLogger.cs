using System;
using System.Diagnostics;
using System.Globalization;

namespace Kudu.TestHarness
{
    public class LatencyLogger : IDisposable
    {
        private string _operationDescription;
        private Stopwatch _stopwatch;

        public LatencyLogger(string operationDescription)
        {
            _operationDescription = operationDescription;
            _stopwatch = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_stopwatch != null)
                {
                    _stopwatch.Stop();
                    TestTracer.Trace("Operation: \"{0}\" took {1:N0} ms", _operationDescription, _stopwatch.ElapsedMilliseconds);
                }
            }
        }
    }
}
