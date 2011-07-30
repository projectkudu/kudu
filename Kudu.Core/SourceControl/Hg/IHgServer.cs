using System;

namespace Kudu.Core.SourceControl.Hg {
    public interface IHgServer {
        string Url { get; }
        bool IsRunning { get; }
        void Start();
        void Stop();
    }
}
