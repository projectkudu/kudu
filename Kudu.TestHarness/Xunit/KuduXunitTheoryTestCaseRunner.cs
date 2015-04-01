using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Kudu.TestHarness.Xunit
{
    public class KuduXunitTheoryTestCaseRunner : XunitTheoryTestCaseRunner
    {
        public KuduXunitTheoryTestCaseRunner(IXunitTestCase testCase,
                                             string displayName,
                                             string skipReason,
                                             object[] constructorArguments,
                                             IMessageSink diagnosticMessageSink,
                                             IMessageBus messageBus,
                                             ExceptionAggregator aggregator,
                                             CancellationTokenSource cancellationTokenSource)
            : base(testCase, displayName, skipReason, constructorArguments, diagnosticMessageSink, messageBus, aggregator, cancellationTokenSource)
        {
        }

        protected override async Task<RunSummary> RunTestAsync()
        {
            var dataDiscoveryException = this.GetDataDiscoveryException();
            if (dataDiscoveryException != null)
                return this.RunTest_DataDiscoveryException();

            var runSummary = new RunSummary();
            var testRunners = this.GetTestRunners();

            foreach (var testRunner in testRunners)
                runSummary.Aggregate(await RunTestAsync(testRunner));

            // Run the cleanup here so we can include cleanup time in the run summary,
            // but save any exceptions so we can surface them during the cleanup phase,
            // so they get properly reported as test case cleanup failures.
            var timer = new ExecutionTimer();
            var cleanupAggregator = this.GetCleanupAggregator();
            var toDispose = this.GetToDispose();
            foreach (var disposable in toDispose)
                timer.Aggregate(() => cleanupAggregator.Run(() => disposable.Dispose()));

            runSummary.Time += timer.Total;
            return runSummary;
        }

        private async Task<RunSummary> RunTestAsync(XunitTestRunner runner)
        {
            var disableRetry = ((KuduXunitTheoryTestCase)TestCase).DisableRetry;
            return await KuduXunitTestRunnerUtils.RunTestAsync(runner, MessageBus, Aggregator, disableRetry);
        }
    }
}
