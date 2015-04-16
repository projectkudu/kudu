using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Kudu.Core.Infrastructure;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Kudu.TestHarness.Xunit
{
    public static class KuduXunitTestRunnerUtils
    {
        // we only limit to 4 tests concurrently each can block upto 4 threads
        public const int MaxParallelThreads = 16;

        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(initialCount: MaxParallelThreads / 4);

        public static async Task<RunSummary> RunTestAsync(XunitTestRunner runner,
                                                          IMessageBus messageBus,
                                                          ExceptionAggregator aggregator,
                                                          bool disableRetry)
        {
            // defense in depth to avoid forever lock
            bool acquired = await _semaphore.WaitAsync(TimeSpan.FromHours(2));

            try
            {
                DelayedMessageBus delayedMessageBus = null;
                RunSummary summary = null;

                if (!acquired)
                {
                    throw new TimeoutException("Wait for thread to run the test timeout!");
                }

                // First run
                if (!disableRetry)
                {
                    // This is really the only tricky bit: we need to capture and delay messages (since those will
                    // contain run status) until we know we've decided to accept the final result;
                    delayedMessageBus = new DelayedMessageBus(messageBus);

                    runner.SetMessageBus(delayedMessageBus);
                    summary = await RunTestInternalAsync(runner);

                    // if succeeded
                    if (summary.Failed == 0 || aggregator.HasExceptions)
                    {
                        delayedMessageBus.Flush(false);
                        return summary;
                    }
                }

                // Final run
                runner.SetMessageBus(new KuduTraceMessageBus(messageBus));
                summary = await RunTestInternalAsync(runner);

                // flush delay messages
                if (delayedMessageBus != null)
                {
                    delayedMessageBus.Flush(summary.Failed == 0 && !aggregator.HasExceptions);
                }

                return summary;
            }
            catch (Exception ex)
            {
                // this is catastrophic
                messageBus.QueueMessage(new TestFailed(runner.GetTest(), 0, null, ex));

                return new RunSummary { Failed = 1, Total = 1 };
            }
            finally
            {
                if (acquired)
                {
                    _semaphore.Release();
                }

                // set to original
                runner.SetMessageBus(messageBus);
            }
        }

        private static async Task<RunSummary> RunTestInternalAsync(XunitTestRunner runner)
        {
            TestTracer.InitializeContext();
            try
            {
                return await runner.RunAsync();
            }
            finally
            {
                TestTracer.FreeContext();
            }
        }

        // making sure all texts are Xml valid
        public static IMessageSinkMessage SanitizeXml(this IMessageSinkMessage message)
        {
            var failed = message as TestFailed;
            if (failed != null)
            {
                return new TestFailed(failed.Test,
                                      failed.ExecutionTime,
                                      XmlUtility.Sanitize(failed.Output),
                                      failed.ExceptionTypes,
                                      failed.Messages == null ? null : failed.Messages.Select(m => XmlUtility.Sanitize(m)).ToArray(),
                                      failed.StackTraces,
                                      failed.ExceptionParentIndices);
            }

            var skipped = message as TestSkipped;
            if (skipped != null)
            {
                skipped = new TestSkipped(skipped.Test, XmlUtility.Sanitize(skipped.Reason));
                skipped.SetOutput(XmlUtility.Sanitize(skipped.Output));
                return skipped;
            }

            var passed = message as TestPassed;
            if (passed != null)
            {
                return new TestPassed(passed.Test, passed.ExecutionTime, XmlUtility.Sanitize(passed.Output));
            }

            return message;
        }

        public class DelayedMessageBus : IMessageBus
        {
            private readonly IMessageBus _innerBus;
            private readonly List<IMessageSinkMessage> _messages;

            public DelayedMessageBus(IMessageBus innerBus)
            {
                _innerBus = innerBus;
                _messages = new List<IMessageSinkMessage>();
            }

            public bool QueueMessage(IMessageSinkMessage message)
            {
                var result = message as TestResultMessage;
                if (result != null && String.IsNullOrEmpty(result.Output))
                {
                    result.SetOutput(TestTracer.GetTraceString());
                }

                lock (_messages)
                {
                    _messages.Add(message);
                }

                // No way to ask the inner bus if they want to cancel without sending them the message, so
                // we just go ahead and continue always.
                return true;
            }

            public void Dispose()
            {
            }

            public void Flush(bool retrySucceeded)
            {
                foreach (var message in _messages)
                {
                    // in case of retry succeeded, convert all failure to skip (ignored)
                    if (retrySucceeded && message is TestFailed)
                    {
                        var failed = (TestFailed)message;
                        var reason = new StringBuilder();
                        reason.AppendLine(String.Join(Environment.NewLine, failed.ExceptionTypes));
                        reason.AppendLine(String.Join(Environment.NewLine, failed.Messages));
                        reason.AppendLine(String.Join(Environment.NewLine, failed.StackTraces));

                        var skipped = new TestSkipped(failed.Test, reason.ToString());
                        skipped.SetOutput(failed.Output);
                        _innerBus.QueueMessage(skipped.SanitizeXml());
                    }
                    else
                    {
                        _innerBus.QueueMessage(message.SanitizeXml());
                    }
                }
            }
        }
    }
}
