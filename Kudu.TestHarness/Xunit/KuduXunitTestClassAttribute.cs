using System;

namespace Kudu.TestHarness.Xunit
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public sealed class KuduXunitTestClassAttribute : Attribute
    {
        public bool DisableRetry { get; set; }
    }
}
