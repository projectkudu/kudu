namespace Kudu.Core.Performance
{
    public class NullProfilerFactory : ProfilerFactory
    {
        public static ProfilerFactory Instance = new NullProfilerFactory();

        private NullProfilerFactory()
            : base(() => NullProfiler.Instance)
        {
        }
    }
}
