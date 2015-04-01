using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kudu.Core.Infrastructure;
using Xunit;

namespace Kudu.Core.Test
{
    public class AsyncStreamWriterFacts
    {
        [Theory]
        [InlineData("pending", new[] { "pending" })]
        [InlineData("line\r\n", new[] { "line" })]
        [InlineData("line\r\n\n", new[] { "line", "" })]
        [InlineData("line1\r\nline2\r\n", new[] { "line1", "line2" })]
        [InlineData("line\npending", new[] { "line", "pending" })]
        [InlineData("\r\npending", new[] { "", "pending" })]
        public async Task AsyncStreamWriterBasicTests(string input, string[] expects)
        {
            var size = 4;
            var encoding = Console.OutputEncoding;
            var actual = new List<string>();
            Action<string> onWrite = data => actual.Add(data);

            // Test
            using (var writer = new AsyncStreamWriter(onWrite, encoding))
            {
                var bytes = encoding.GetBytes(input);
                for (int i = 0; i < bytes.Length; i += size)
                {
                    await writer.WriteAsync(bytes, i, Math.Min(size, bytes.Length - i), default(CancellationToken));
                }
            }

            // Assert
            Assert.Equal(expects, actual);
        }
    }
}
