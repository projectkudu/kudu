using System;
using Kudu.Core.Tracing;
using Xunit;

namespace Kudu.Core.Test
{
    public class SASUriRedactedMessageTest
    {
        [Fact]
        public void RedactSASUriInMessageTest()
        {
            // Test the message container signature key in the SAS Uri
            string signatureKey = "fake_signature";
            string messageWithFakeSASUri = $"This is a test message with fake SAS Uri https://fakebstore.blob.core.windows.net/testcontainer/invalidfile.zip?sp=r&st=2024-02-06T06:19:48Z&se=2024-02-06T14:19:48Z&spr=https&sv=2022-11-02&sr=b&sig={signatureKey}";
            var redactedSASMessage = KuduEventSource.RedactSasUriIfPresent(messageWithFakeSASUri);

            // Make sure the signature key has been REDACTED
            Assert.Contains("sig=REDACTED", redactedSASMessage, StringComparison.OrdinalIgnoreCase);

            // Make sure that the reacted message doesn't contain the Signature key in the SAS uri
            Assert.True(!redactedSASMessage.Contains(signatureKey));

            // Test the message with no SAS Uri
            string messageWithNoSASUri = "This log message has no SAS URI with signature key and should remain unchanged";
            string outMsg = KuduEventSource.RedactSasUriIfPresent(messageWithNoSASUri);
            Assert.True(string.Equals(messageWithNoSASUri, outMsg));

            // Test for null, the output should remain null
            string nullMsg = null;
            outMsg = KuduEventSource.RedactSasUriIfPresent(nullMsg);
            Assert.True(string.Equals(nullMsg, outMsg));

            // Test for empty message, the output should remain empty
            string emptyMsg = string.Empty;
            outMsg = KuduEventSource.RedactSasUriIfPresent(emptyMsg);
            Assert.True(string.Equals(emptyMsg, outMsg));
        }
    }
}
