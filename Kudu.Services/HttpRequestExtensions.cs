using System.IO;
using System.IO.Compression;
using System.Web;

namespace Kudu.Services
{
    public static class HttpRequestExtensions
    {
        public static Stream GetInputStream(this HttpRequestBase request)
        {
            var contentEncoding = request.Headers["Content-Encoding"];

            if (contentEncoding != null && contentEncoding.Contains("gzip"))
            {
                return new GZipStream(request.GetBufferlessInputStream(), CompressionMode.Decompress);
            }

            return request.GetBufferlessInputStream();
        }
    }
}
