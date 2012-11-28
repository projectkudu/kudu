using System;
using System.IO;
using System.Net.Http.Headers;
using Kudu.Common;
using Kudu.Services.Infrastructure;

namespace Kudu.Services.ByteRanges
{
    /// <summary>
    /// Stream which only exposes a read-only only range view of an inner stream.
    /// </summary>
    internal class ByteRangeStream : DelegatingStream
    {
        // The offset stream position at which the range starts.
        private readonly long _lowerbounds;

        // The total number of bytes within the range. 
        private readonly long _totalCount;

        // The current number of bytes read into the range
        private long _currentCount;

        public ByteRangeStream(Stream innerStream, RangeItemHeaderValue range)
            : base(innerStream)
        {
            if (range == null)
            {
                throw new ArgumentNullException("range");
            }
            if (!innerStream.CanSeek)
            {
                throw new ArgumentException(RS.Format(Resources.ByteRangeStreamNotSeekable, typeof(ByteRangeStream).Name), "innerStream");
            }
            if (innerStream.Length < 1)
            {
                throw new ArgumentOutOfRangeException("innerStream", innerStream.Length,
                    RS.Format(Resources.ByteRangeStreamEmpty, typeof(ByteRangeStream).Name));
            }
            if (range.From.HasValue && range.From.Value > innerStream.Length)
            {
                throw new ArgumentOutOfRangeException("range", range.From,
                    RS.Format(Resources.ByteRangeStreamInvalidFrom, innerStream.Length));
            }

            // Ranges are inclusive so 0-9 means the first 10 bytes
            long maxLength = innerStream.Length - 1;
            long upperbounds;
            if (range.To.HasValue)
            {
                if (range.From.HasValue)
                {
                    // e.g bytes=0-499 (the first 500 bytes offsets 0-499)
                    upperbounds = Math.Min(range.To.Value, maxLength);
                    _lowerbounds = range.From.Value;
                }
                else
                {
                    // e.g bytes=-500 (the final 500 bytes)
                    upperbounds = maxLength;
                    _lowerbounds = Math.Max(innerStream.Length - range.To.Value, 0);
                }
            }
            else
            {
                if (range.From.HasValue)
                {
                    // e.g bytes=500- (from byte offset 500 and up)
                    upperbounds = maxLength;
                    _lowerbounds = range.From.Value;
                }
                else
                {
                    // e.g. bytes=- (invalid so will never get here)
                    upperbounds = maxLength;
                    _lowerbounds = 0;
                }
            }

            _totalCount = upperbounds - _lowerbounds + 1;
            ContentRange = new ContentRangeHeaderValue(_lowerbounds, upperbounds, innerStream.Length);
        }

        public ContentRangeHeaderValue ContentRange { get; private set; }

        public override long Length
        {
            get { return _totalCount; }
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return base.BeginRead(buffer, offset, PrepareStreamForRangeRead(count), callback, state);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return base.Read(buffer, offset, PrepareStreamForRangeRead(count));
        }

        public override int ReadByte()
        {
            int effectiveCount = PrepareStreamForRangeRead(1);
            if (effectiveCount <= 0)
            {
                return -1;
            }
            return base.ReadByte();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException(Resources.ByteRangeStreamReadOnly);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException(Resources.ByteRangeStreamReadOnly);
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            throw new NotSupportedException(Resources.ByteRangeStreamReadOnly);
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            throw new NotSupportedException(Resources.ByteRangeStreamReadOnly);
        }

        public override void WriteByte(byte value)
        {
            throw new NotSupportedException(Resources.ByteRangeStreamReadOnly);
        }

        /// <summary>
        /// Gets the 
        /// </summary>
        /// <param name="count">The count requested to be read by the caller.</param>
        /// <returns>The remaining bytes to read within the range defined for this stream.</returns>
        private int PrepareStreamForRangeRead(int count)
        {
            long effectiveCount = Math.Min(count, _totalCount - _currentCount);
            if (effectiveCount > 0)
            {
                // Check if we should update the stream position
                long position = InnerStream.Position;
                if (_lowerbounds + _currentCount != position)
                {
                    InnerStream.Position = _lowerbounds + _currentCount;
                }

                // Update current number of bytes read
                _currentCount += effectiveCount;
            }

            // Effective count can never be bigger than int
            return (int)effectiveCount;
        }
    }
}
