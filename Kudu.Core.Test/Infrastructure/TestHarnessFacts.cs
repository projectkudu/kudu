using System;
using System.Threading.Tasks;
using Kudu.TestHarness.Xunit;
using Xunit;

namespace Kudu.Core.Test
{
    public class TestHarnessFacts
    {
        [Fact]
        public void SkippedSyncTest()
        {
            throw new KuduXunitTestSkippedException("Testing SkippedSyncTest");
        }

        [Fact]
        public void SkippedAsyncTest()
        {
            SkippedAsync().Wait();
            throw new InvalidOperationException("Should not reach here");
        }

        private async Task SkippedAsync()
        {
            await Task.Delay(100);
            throw new KuduXunitTestSkippedException("Testing SkippedAsyncTest");
        }
    }
}
