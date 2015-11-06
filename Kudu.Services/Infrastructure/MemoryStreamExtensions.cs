using System.IO;
using System.Net.Http;
using System.Text;

namespace Kudu.Services.Infrastructure
{
    public static class MemoryStreamExtensions
    {
        public static HttpContent AsContent(this MemoryStream stream)
        {
            return new ByteArrayContent(stream.GetBuffer(), 0, (int)stream.Length);
        }

        public static string AsString(this MemoryStream stream, Encoding encoding = null)
        {
            if (stream.Length > 0)
            {
                encoding = encoding ?? Encoding.UTF8;
                return encoding.GetString(stream.GetBuffer(), 0, (int)stream.Length);
            }
            return string.Empty;
        }
    }
}
