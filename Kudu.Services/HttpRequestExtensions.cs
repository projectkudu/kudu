using System.IO;
using System.Web;
using Ionic.Zlib;

namespace Kudu.Services
{
    public static class HttpRequestExtensions
    {
        public static Stream GetInputStream(this HttpRequestBase request)
        {
            var contentEncoding = request.Headers["Content-Encoding"];

            if (contentEncoding != null && contentEncoding.Contains("gzip"))
            {
                return new GZipStream(request.InputStream, CompressionMode.Decompress);
            }

            return request.InputStream;
        }
    }
}
