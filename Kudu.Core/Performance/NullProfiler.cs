using System;
using Kudu.Contracts;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Performance
{
    public class NullProfiler : IProfiler
    {
        public static IProfiler Instance = new NullProfiler();

        private NullProfiler()
        {
        }

        public IDisposable Step(string value)
        {
            return new DisposableAction(() => { });
        }
    }
}
