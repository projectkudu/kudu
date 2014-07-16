using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using Kudu.Contracts.Tracing;
using Kudu.Core.Tracing;

namespace Kudu.Services.Infrastructure
{
    public static class ZipStreamContent
    {
        public static PushStreamContent Create(string fileName, ITracer tracer, Action<ZipArchive> onZip)
        {
            var content = new PushStreamContent((outputStream, httpContent, transportContext) =>
            {
                using (tracer.Step("ZipStreamContent.OnZip"))
                {
                    try
                    {
                        using (var zip = new ZipArchive(new StreamWrapper(outputStream), ZipArchiveMode.Create, leaveOpen: false))
                        {
                            onZip(zip);
                        }
                    }
                    catch (Exception ex)
                    {
                        tracer.TraceError(ex);
                        throw;
                    }
                }
            });
            content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
            content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment");
            content.Headers.ContentDisposition.FileName = fileName;
            return content;
        }

        // this wraps the read-only HttpResponseStream to support ZipArchive Position getter.
        public class StreamWrapper : DelegatingStream
        {
            private long _position = 0;

            public StreamWrapper(Stream stream)
                : base(stream)
            {
            }

            public override long Position
            {
                get { return _position; }
                set { throw new NotSupportedException(); }
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                _position += count;
                base.Write(buffer, offset, count);
            }

            public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
            {
                _position += count;
                return base.BeginWrite(buffer, offset, count, callback, state);
            }
        }
    }
}