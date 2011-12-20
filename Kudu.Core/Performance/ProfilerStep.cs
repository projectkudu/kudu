using System.Collections.Generic;
using System.Diagnostics;

namespace Kudu.Core.Performance
{
    public class ProfilerStep
    {
        public string Title { get; private set; }
        public long ElapsedMilliseconds { get; private set; }
        public List<ProfilerStep> Children { get; private set; }

        private Stopwatch _stopWatch;

        public ProfilerStep(string title)
        {
            Title = title;
            Children = new List<ProfilerStep>();
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
