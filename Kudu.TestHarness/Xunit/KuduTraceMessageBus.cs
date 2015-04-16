using System;
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

            return _innerBus.QueueMessage(message.SanitizeXml());
        }

        public void Dispose()
        {
            _innerBus.Dispose();
        }
    }
}
