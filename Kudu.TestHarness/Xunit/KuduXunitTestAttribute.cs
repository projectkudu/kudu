using System;

namespace Kudu.TestHarness.Xunit
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class KuduXunitTestAttribute : Attribute
    {
        public bool PrivateOnly { get; set; }
        public int MinAntaresVersion { get; set; }
    }
}
