using System;
using Kudu.Contracts;

namespace Kudu.Core.Performance
{
    public class ProfilerFactory : IProfilerFactory
    {
        private readonly Func<IProfiler> _factory;

        public ProfilerFactory(Func<IProfiler> factory)
        {
            _factory = factory;
        }

        public IProfiler GetProfiler()
        {
            return _factory();
        }
    }
}
