using System.IO;
using System.Net.Http;

namespace Kudu.Services.Infrastructure
{
    public static class MemoryStreamExtensions
    {
        public static HttpContent AsContent(this MemoryStream stream)
        {
            return new ByteArrayContent(stream.GetBuffer(), 0, (int)stream.Length);
        }
    }
}
