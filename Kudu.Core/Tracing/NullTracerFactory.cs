namespace Kudu.Core.Tracing
{
    public class NullTracerFactory : TracerFactory
    {
        public static TracerFactory Instance = new NullTracerFactory();

        private NullTracerFactory()
            : base(() => NullTracer.Instance)
        {
        }
    }
}
