using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Kudu.Common;

namespace Kudu.Services.ByteRanges
{
    /// <summary>
    /// <see cref="HttpContent"/> implementation which provides a byte range view over a stream used to generate HTTP
    /// 206 (Partial Content) byte range responses. The <see cref="ByteRangeStreamContent"/> supports one or more 
    /// byte ranges regardless of whether the ranges are consecutive or not. If there is only one range then a 
    /// single partial response body containing a Content-Range header is generated. If there are more than one
    /// ranges then a multipart/byteranges response is generated where each body part contains a range indicated
    /// by the associated Content-Range header field.
    /// </summary>
    internal class ByteRangeStreamContent : HttpContent
    {
        private const string SupportedRangeUnit = "bytes";
        private const string ByteRangesContentSubtype = "byteranges";
        private const int DefaultBufferSize = 4096;
        private const int MinBufferSize = 1;

        private readonly Stream _content;
        private readonly long _start;
        private readonly HttpContent _byteRangeContent;
        private bool _disposed;

        /// <summary>
        /// <see cref="HttpContent"/> implementation which provides a byte range view over a stream used to generate HTTP
        /// 206 (Partial Content) byte range responses. If none of the requested ranges overlap with the current extend 
        /// of the selected resource represented by the <paramref name="content"/> parameter then an 
        /// <see cref="InvalidByteRangeException"/> is thrown indicating the valid Content-Range of the content. 
        /// </summary>
        /// <param name="content">The stream over which to generate a byte range view.</param>
        /// <param name="range">The range or ranges, typically obtained from the Range HTTP request header field.</param>
        /// <param name="mediaType">The media type of the content stream.</param>
        public ByteRangeStreamContent(Stream content, RangeHeaderValue range, string mediaType)
            : this(content, range, new MediaTypeHeaderValue(mediaType), DefaultBufferSize)
        {
        }

        /// <summary>
        /// <see cref="HttpContent"/> implementation which provides a byte range view over a stream used to generate HTTP
        /// 206 (Partial Content) byte range responses. If none of the requested ranges overlap with the current extend 
        /// of the selected resource represented by the <paramref name="content"/> parameter then an 
        /// <see cref="InvalidByteRangeException"/> is thrown indicating the valid Content-Range of the content. 
        /// </summary>
        /// <param name="content">The stream over which to generate a byte range view.</param>
        /// <param name="range">The range or ranges, typically obtained from the Range HTTP request header field.</param>
        /// <param name="mediaType">The media type of the content stream.</param>
        /// <param name="bufferSize">The buffer size used when copying the content stream.</param>
        public ByteRangeStreamContent(Stream content, RangeHeaderValue range, string mediaType, int bufferSize)
            : this(content, range, new MediaTypeHeaderValue(mediaType), bufferSize)
        {
        }

        /// <summary>
        /// <see cref="HttpContent"/> implementation which provides a byte range view over a stream used to generate HTTP
        /// 206 (Partial Content) byte range responses. If none of the requested ranges overlap with the current extend 
        /// of the selected resource represented by the <paramref name="content"/> parameter then an 
        /// <see cref="InvalidByteRangeException"/> is thrown indicating the valid Content-Range of the content. 
        /// </summary>
        /// <param name="content">The stream over which to generate a byte range view.</param>
        /// <param name="range">The range or ranges, typically obtained from the Range HTTP request header field.</param>
        /// <param name="mediaType">The media type of the content stream.</param>
        public ByteRangeStreamContent(Stream content, RangeHeaderValue range, MediaTypeHeaderValue mediaType)
            : this(content, range, mediaType, DefaultBufferSize)
        {
        }

        /// <summary>
        /// <see cref="HttpContent"/> implementation which provides a byte range view over a stream used to generate HTTP
        /// 206 (Partial Content) byte range responses. If none of the requested ranges overlap with the current extend 
        /// of the selected resource represented by the <paramref name="content"/> parameter then an 
        /// <see cref="InvalidByteRangeException"/> is thrown indicating the valid Content-Range of the content. 
        /// </summary>
        /// <param name="content">The stream over which to generate a byte range view.</param>
        /// <param name="range">The range or ranges, typically obtained from the Range HTTP request header field.</param>
        /// <param name="mediaType">The media type of the content stream.</param>
        /// <param name="bufferSize">The buffer size used when copying the content stream.</param>
        public ByteRangeStreamContent(Stream content, RangeHeaderValue range, MediaTypeHeaderValue mediaType, int bufferSize)
        {
            if (content == null)
            {
                throw new ArgumentNullException("content");
            }
            if (!content.CanSeek)
            {
                throw new ArgumentException("content", RS.Format(Resources.ByteRangeStreamNotSeekable, typeof(ByteRangeStreamContent).Name));
            }
            if (range == null)
            {
                throw new ArgumentNullException("range");
            }
            if (mediaType == null)
            {
                throw new ArgumentNullException("mediaType");
            }
            if (bufferSize < MinBufferSize)
            {
                throw new ArgumentOutOfRangeException("bufferSize", bufferSize, RS.Format(Resources.ArgumentMustBeGreaterThanOrEqualTo, MinBufferSize));
            }
            if (!range.Unit.Equals(SupportedRangeUnit, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(RS.Format(Resources.ByteRangeStreamContentNotBytesRange, range.Unit, SupportedRangeUnit), "range");
            }

            try
            {
                // If we have more than one range then we use a multipart/byteranges content type as wrapper.
                // Otherwise we use a non-multipart response.
                if (range.Ranges.Count > 1)
                {
                    // Create Multipart content and copy headers to this content
                    MultipartContent rangeContent = new MultipartContent(ByteRangesContentSubtype);
                    _byteRangeContent = rangeContent;

                    foreach (RangeItemHeaderValue rangeValue in range.Ranges)
                    {
                        try
                        {
                            ByteRangeStream rangeStream = new ByteRangeStream(content, rangeValue);
                            HttpContent rangeBodyPart = new StreamContent(rangeStream, bufferSize);
                            rangeBodyPart.Headers.ContentType = mediaType;
                            rangeBodyPart.Headers.ContentRange = rangeStream.ContentRange;
                            rangeContent.Add(rangeBodyPart);
                        }
                        catch (ArgumentOutOfRangeException)
                        {
                            // We ignore range errors until we check that we have at least one valid range
                        }
                    }

                    // If no overlapping ranges were found then stop
                    if (!rangeContent.Any())
                    {
                        ContentRangeHeaderValue actualContentRange = new ContentRangeHeaderValue(content.Length);
                        string msg = RS.Format(Resources.ByteRangeStreamNoneOverlap, range.ToString());
                        throw new InvalidByteRangeException(actualContentRange, msg);
                    }
                }
                else if (range.Ranges.Count == 1)
                {
                    try
                    {
                        ByteRangeStream rangeStream = new ByteRangeStream(content, range.Ranges.First());
                        _byteRangeContent = new StreamContent(rangeStream, bufferSize);
                        _byteRangeContent.Headers.ContentType = mediaType;
                        _byteRangeContent.Headers.ContentRange = rangeStream.ContentRange;
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        ContentRangeHeaderValue actualContentRange = new ContentRangeHeaderValue(content.Length);
                        string msg = RS.Format(Resources.ByteRangeStreamNoOverlap, range.ToString());
                        throw new InvalidByteRangeException(actualContentRange, msg);
                    }
                }
                else
                {
                    throw new ArgumentException(Resources.ByteRangeStreamContentNoRanges, "range");
                }

                // Copy headers from byte range content so that we get the right content type etc.
                foreach (KeyValuePair<string, IEnumerable<string>> header in _byteRangeContent.Headers)
                {
                    Headers.TryAddWithoutValidation(header.Key, header.Value);
                }

                _content = content;
                _start = content.Position;
            }
            catch
            {
                if (_byteRangeContent != null)
                {
                    _byteRangeContent.Dispose();
                }
                throw;
            }
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {
            // Reset stream to start position
            _content.Position = _start;

            // Copy result to output
            return _byteRangeContent.CopyToAsync(stream);
        }

        protected override bool TryComputeLength(out long length)
        {
            long? contentLength = _byteRangeContent.Headers.ContentLength;
            if (contentLength.HasValue)
            {
                length = contentLength.Value;
                return true;
            }

            length = -1;
            return false;
        }

        protected override void Dispose(bool disposing)
        {
            Contract.Assert(_byteRangeContent != null);
            if (disposing)
            {
                if (!_disposed)
                {
                    _byteRangeContent.Dispose();
                    _content.Dispose();
                    _disposed = true;
                }
            }
            base.Dispose(disposing);
        }
    }
}
