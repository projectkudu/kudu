using System;
using System.IO;
using System.Net.Http.Headers;
using Kudu.Services.Infrastructure;

namespace Kudu.Services.ByteRanges
{
    /// <summary>
    /// Stream which only exposes a read-only only range view of an 
    /// inner stream.
    /// </summary>
    internal class ByteRangeStream : DelegatingStream
    {
        private readonly long _lowerbounds;
        private readonly long _length;
        private long _totalCount;

        public ByteRangeStream(Stream innerStream, RangeItemHeaderValue range)
            : base(innerStream)
        {
            if (range == null)
            {
                throw new ArgumentNullException("range");
            }
            if (!innerStream.CanSeek)
            {
                string msg = String.Format("The stream over which '{0}' provides a range view must be seekable.", typeof(ByteRangeStream).Name);
                throw new ArgumentException(msg, "innerStream");
            }
            if (innerStream.Length < 1)
            {
                string msg = String.Format("The stream over which '{0}' provides a range view must have a length greater than or equal to 1.", typeof(ByteRangeStream).Name);
                throw new ArgumentOutOfRangeException("innerStream", innerStream.Length, msg);
            }
            if (range.From.HasValue && range.From.Value > innerStream.Length)
            {
                string msg = String.Format("The 'From' value of the range must be less than or equal to {0}.", innerStream.Length);
                throw new ArgumentOutOfRangeException("range", range.From, msg);
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

            _length = upperbounds - _lowerbounds + 1;
            ContentRange = new ContentRangeHeaderValue(_lowerbounds, upperbounds, innerStream.Length);
        }

        public ContentRangeHeaderValue ContentRange { get; private set; }

        public override long Length
        {
            get { return _length; }
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return base.BeginRead(buffer, offset, GetEffectiveCount(count), callback, state);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return base.Read(buffer, offset, GetEffectiveCount(count));
        }

        public override int ReadByte()
        {
            int effectiveCount = GetEffectiveCount(1);
            if (effectiveCount == 0)
            {
                return -1;
            }
            return base.ReadByte();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException("This is a read-only stream.");
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException("This is a read-only stream.");
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            throw new NotSupportedException("This is a read-only stream.");
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            throw new NotSupportedException("This is a read-only stream.");
        }

        public override void WriteByte(byte value)
        {
            throw new NotSupportedException("This is a read-only stream.");
        }

        private int GetEffectiveCount(int count)
        {
            long effectiveCount = Math.Min(count, _length - _totalCount);
            if (effectiveCount != 0)
            {
                long position = InnerStream.Position;
                if (_lowerbounds + _totalCount != position)
                {
                    InnerStream.Position = _lowerbounds + _totalCount;
                }
                _totalCount += effectiveCount;
            }
            return (int)effectiveCount;
        }
    }
}
