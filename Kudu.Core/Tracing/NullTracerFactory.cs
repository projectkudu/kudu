namespace Kudu.Core.Tracing
{
    public sealed class NullTracerFactory : TracerFactory
    {
        public static readonly TracerFactory Instance = new NullTracerFactory();

        private NullTracerFactory()
            : base(() => NullTracer.Instance)
        {
        }
    }
}
