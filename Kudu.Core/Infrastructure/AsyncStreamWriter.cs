using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Kudu.Core.Infrastructure
{
    // this is used specifically for push model.
    // onWrite is called for every new line written.
    public class AsyncStreamWriter : Stream
    {
        private static readonly Task _completed = Task.FromResult(0);

        private readonly Action<string> _onWrite;
        private readonly Encoding _encoding;
        private readonly StringBuilder _strb;
        private bool _hasData;

        public AsyncStreamWriter(Action<string> onWrite, Encoding encoding)
        {
            _onWrite = onWrite;
            _encoding = encoding;
            _strb = new StringBuilder();
        }

        public override bool CanRead
        {
            get { return false; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return true; }
        }

        public override void Close()
        {
            if (_hasData)
            {
                _onWrite(_strb.ToString());
            }

            base.Close();
        }

        public override void Flush()
        {
            // No-op
        }

        public override long Length
        {
            get { throw new NotSupportedException(); }
        }

        public override long Position
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            char[] chars = _encoding.GetChars(buffer, offset, count);
            for (int i = 0; i < chars.Length; ++i)
            {
                if (chars[i] == '\r' || chars[i] == '\n')
                {
                    if (chars[i] == '\r' && (i + 1 < chars.Length) && chars[i + 1] == '\n')
                    {
                        ++i;
                    }

                    _onWrite(_strb.ToString());
                    _strb.Clear();
                    _hasData = false;
                    continue;
                }

                _strb.Append(chars[i]);
                _hasData = true;
            }

            return _completed;
        }
    }
}
