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
            if (_stopwatch != null)
            {
                _stopwatch.Stop();
                Trace.WriteLine(String.Format(CultureInfo.CurrentCulture, "Operation: \"{0}\" took {1:N0} ms", _operationDescription, _stopwatch.ElapsedMilliseconds));
            }
        }
    }
}
