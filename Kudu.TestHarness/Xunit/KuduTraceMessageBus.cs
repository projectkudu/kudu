using System;
using System.Linq;
using Kudu.Core.Infrastructure;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Kudu.TestHarness.Xunit
{
    public class KuduTraceMessageBus : IMessageBus
    {
        private readonly IMessageBus _innerBus;

        public KuduTraceMessageBus(IMessageBus innerBus)
        {
            _innerBus = innerBus;
        }

        public bool QueueMessage(IMessageSinkMessage message)
        {
            var result = message as TestResultMessage;
            if (result != null && String.IsNullOrEmpty(result.Output))
            {
                result.SetOutput(TestTracer.GetTraceString());
            }

            var failed = message as TestFailed;
            if (failed != null && failed.Messages != null && failed.Messages.Length > 0)
            {
                // this is to workaround failed.Messages contains invalid Xml chars
                message = new TestFailed(failed.Test, 
                                         failed.ExecutionTime, 
                                         failed.Output,
                                         failed.ExceptionTypes,
                                         failed.Messages.Select(m => XmlUtility.Sanitize(m)).ToArray(),
                                         failed.StackTraces,
                                         failed.ExceptionParentIndices);
            }

            return _innerBus.QueueMessage(message);
        }

        public void Dispose()
        {
            _innerBus.Dispose();
        }
    }
}
