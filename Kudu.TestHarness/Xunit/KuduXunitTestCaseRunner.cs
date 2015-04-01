using System.Threading;
using System.Threading.Tasks;
using Xunit.Sdk;

namespace Kudu.TestHarness.Xunit
{
    public class KuduXunitTestCaseRunner : XunitTestCaseRunner
    {
        public KuduXunitTestCaseRunner(IXunitTestCase testCase,
                                       string displayName,
                                       string skipReason,
                                       object[] constructorArguments,
                                       object[] testMethodArguments,
                                       IMessageBus messageBus,
                                       ExceptionAggregator aggregator,
                                       CancellationTokenSource cancellationTokenSource)
            : base(testCase, displayName, skipReason, constructorArguments, testMethodArguments, messageBus, aggregator, cancellationTokenSource)
        {
        }

        protected override async Task<RunSummary> RunTestAsync()
        {
            var test = new XunitTest(TestCase, DisplayName);
            var aggregator = new ExceptionAggregator(Aggregator);
            var disableRetry = ((KuduXunitTestCase)TestCase).DisableRetry;
            var runner = new XunitTestRunner(test, MessageBus, TestClass, ConstructorArguments, TestMethod, TestMethodArguments, SkipReason, BeforeAfterAttributes, aggregator, CancellationTokenSource);
            return await KuduXunitTestRunnerUtils.RunTestAsync(runner, MessageBus, aggregator, disableRetry);
        }
    }
}