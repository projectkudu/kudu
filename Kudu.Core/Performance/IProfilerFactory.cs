using Kudu.Contracts;

namespace Kudu.Core.Performance
{
    public interface IProfilerFactory
    {
        IProfiler CreateProfiler();
    }
}
