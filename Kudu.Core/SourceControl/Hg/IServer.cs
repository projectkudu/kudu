using System;

namespace Kudu.Core.SourceControl.Hg {
    public interface IServer {
        string Url { get; }
        bool IsRunning { get; }
        void Start();
        void Stop();
    }
}
