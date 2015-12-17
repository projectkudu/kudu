using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Kudu.TestHarness.Xunit
{
    public class KuduXunitTheoryTestCase : XunitTheoryTestCase
    {
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Called by the de-serializer", true)]
        public KuduXunitTheoryTestCase() { }

        public KuduXunitTheoryTestCase(IMessageSink diagnosticMessageSink,
                                       TestMethodDisplay defaultMethodDisplay,
                                       ITestMethod testMethod,
                                       IAttributeInfo testAttribute)
            : base(diagnosticMessageSink, defaultMethodDisplay, testMethod)
        {
        }

        public override Task<RunSummary> RunAsync(IMessageSink diagnosticMessageSink,
                                                  IMessageBus messageBus,
                                                  object[] constructorArguments,
                                                  ExceptionAggregator aggregator,
                                                  CancellationTokenSource cancellationTokenSource)
        {
            return new KuduXunitTheoryTestCaseRunner(this, DisplayName, SkipReason, constructorArguments, diagnosticMessageSink, messageBus, aggregator, cancellationTokenSource).RunAsync();
        }
    }
}
