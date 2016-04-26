using System.IO;
using System.IO.Compression;
using System.Web;
using Kudu.Core.Helpers;

namespace Kudu.Services
{
    public static class HttpRequestExtensions
    {
        public static Stream GetInputStream(this HttpRequestBase request)
        {
            var contentEncoding = request.Headers["Content-Encoding"];
            if (contentEncoding != null && contentEncoding.Contains("gzip"))
            {
                // https://github.com/mono/mono/pull/1914
                // GetBufferlessInputStream will come in next release (current 4.2.1)
                return new GZipStream(request.InputStream, CompressionMode.Decompress);
            }

            // https://github.com/mono/mono/pull/1914
            // GetBufferlessInputStream will come in next release (current 4.2.1)
            return request.InputStream;
        }
    }
}
