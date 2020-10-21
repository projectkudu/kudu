using System;

namespace Kudu.TestHarness.Xunit
{
    [Serializable]
    public class KuduXunitTestSkippedException : Exception
    {
        public KuduXunitTestSkippedException(string reason)
            : base(reason)
        {
        }

        public KuduXunitTestSkippedException(string reason, Exception innerException)
            : base(reason, innerException)
        {
        }
    }
}