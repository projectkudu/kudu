using System;
using Kudu.Contracts.Tracing;
using Xunit;
using Xunit.Extensions;

namespace Kudu.Core.Test.Tracing
{
    public class TracingTests
    {
        [Theory]
        [InlineData(true, TraceExtensions.AlwaysTrace)]
        [InlineData(true, TraceExtensions.TraceLevelKey)]
        [InlineData(true, "Max-Forwards")]
        [InlineData(true, "X-ARR-LOG-ID")]
        [InlineData(false, "url")]
        [InlineData(false, "method")]
        [InlineData(false, "type")]
        [InlineData(false, "Host")]
        public void TracingAttributeBlacklistTests(bool expected, string header)
        {
            Assert.Equal(expected, TraceExtensions.IsNonDisplayableAttribute(header));
        }
    }
}
