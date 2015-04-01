using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Kudu.TestHarness.Xunit
{
    [Serializable]
    public class KuduXunitTestCase : XunitTestCase
    {
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Called by the de-serializer", true)]
        public KuduXunitTestCase() { }

        public KuduXunitTestCase(IMessageSink diagnosticMessageSink,
                                 TestMethodDisplay testMethodDisplay,
                                 ITestMethod testMethod,
                                 object[] testMethodArguments,
                                 IAttributeInfo testAttribute)
            : base(diagnosticMessageSink, testMethodDisplay, testMethod, testMethodArguments)
        {
            DisableRetry = testAttribute == null ? true : testAttribute.GetNamedArgument<bool>("DisableRetry");
        }

        public bool DisableRetry
        {
            get;
            set;
        }

        public override Task<RunSummary> RunAsync(IMessageSink diagnosticMessageSink,
                                                 IMessageBus messageBus,
                                                 object[] constructorArguments,
                                                 ExceptionAggregator aggregator,
                                                 CancellationTokenSource cancellationTokenSource)
        {
            return new KuduXunitTestCaseRunner(this, DisplayName, SkipReason, constructorArguments, TestMethodArguments, messageBus, aggregator, cancellationTokenSource).RunAsync();
        }

        public override void Serialize(IXunitSerializationInfo data)
        {
            base.Serialize(data);

            data.AddValue("DisableRetry", DisableRetry);
        }

        public override void Deserialize(IXunitSerializationInfo data)
        {
            base.Deserialize(data);

            DisableRetry = data.GetValue<bool>("DisableRetry");
        }
    }
}