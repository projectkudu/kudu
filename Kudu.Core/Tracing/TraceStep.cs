using System.Collections.Generic;
using System.Diagnostics;

namespace Kudu.Core.Tracing
{
    public class TraceStep
    {
        public string Title { get; private set; }
        public long ElapsedMilliseconds { get; private set; }
        public List<TraceStep> Children { get; private set; }

        private Stopwatch _stopWatch;

        public TraceStep(string title)
        {
            Title = title;
            Children = new List<TraceStep>();
        }

        public void Start()
        {
            _stopWatch = Stopwatch.StartNew();
        }

        public void Stop()
        {
            _stopWatch.Stop();
            ElapsedMilliseconds = _stopWatch.ElapsedMilliseconds;
        }
    }
}
