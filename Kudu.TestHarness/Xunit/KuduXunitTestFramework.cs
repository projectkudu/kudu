using Xunit.Abstractions;
using Xunit.Sdk;

namespace Kudu.TestHarness.Xunit
{
    public class KuduXunitTestFramework : XunitTestFramework
    {
        public KuduXunitTestFramework(IMessageSink diagnosticMessageSink)
            : base(diagnosticMessageSink) 
        { 
        }

        protected override ITestFrameworkDiscoverer CreateDiscoverer(IAssemblyInfo assemblyInfo)
        {
            return new KuduXunitTestFrameworkDiscoverer(assemblyInfo, SourceInformationProvider, DiagnosticMessageSink);
        }
    }
}
