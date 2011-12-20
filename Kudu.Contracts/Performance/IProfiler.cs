using System;

namespace Kudu.Contracts
{
    public interface IProfiler
    {
        IDisposable Step(string value);
    }
}
