using System;
using System.Text;

namespace Kudu.Core.Infrastructure
{
    internal class StringReader : IStringReader
    {
        private string _raw;
        private int _index;

        public StringReader(string raw)
        {
            if (raw == null)
            {
                throw new ArgumentNullException("raw");
            }
            _raw = raw;
        }

        public bool Done
        {
            get
            {
                return _index >= _raw.Length;
            }
        }

        private char Current
        {
            get
            {
                return Done ? '\0' : _raw[_index];
            }
        }

        public string ReadLine()
        {
            string value = ReadUntil('\n');
            var ch = Read();
            if (String.IsNullOrEmpty(value) && ch == '\0')
            {
                return null;
            }
            else if (ch == '\0')
            {
                return value;
            }

            return value + ch;
        }

        public string Read(int n)
        {
            if (_index + n <= _raw.Length)
            {
                string value = _raw.Substring(_index, n);
                Skip(n);
                return value;
            }
            return null;
        }

        public char Read()
        {
            char ch = Current;
            Skip();
            return ch;
        }

        public int ReadInt()
        {
            string value = ReadUntil(ch => !Char.IsDigit(ch));
            return String.IsNullOrEmpty(value) ? 0 : Int32.Parse(value);
        }

        public string ReadUntilWhitespace()
        {
            return ReadUntil(ch => Char.IsWhiteSpace(ch));
        }

        public string ReadUntil(char delimiter)
        {
            string result;
            TryReadUntil(delimiter, out result);
            return result;
        }

        public string ReadUntil(string value)
        {
            string result;
            TryReadUntil(value, out result);
            return result;
        }

        public string ReadUntil(Func<char, bool> condition)
        {
            string result;
            TryReadUntil(condition, out result);
            return result;
        }

        public bool TryReadUntil(Func<char, bool> condition, out string result)
        {
            var sb = new StringBuilder();
            while (!Done)
            {
                if (condition(Current))
                {
                    result = sb.ToString();
                    return true;
                }
                sb.Append(Current);
                Skip();
            }
            result = sb.ToString();
            if (String.IsNullOrEmpty(result))
            {
                result = null;
            }
            return false;
        }

        public bool TryReadUntil(char delimiter, out string result)
        {
            return TryReadUntil(ch => ch == delimiter, out result);
        }

        public bool TryReadUntil(string value, out string result)
        {
            var sb = new StringBuilder();
            result = null;
            while (!Done)
            {
                if (Peek(value))
                {
                    result = sb.ToString();
                    return true;
                }
                sb.Append(Current);
                Skip();
            }
            result = sb.ToString();
            if (String.IsNullOrEmpty(result))
            {
                result = null;
            }
            return false;
        }

        public char Peek()
        {
            return Current;
        }

        public bool Peek(string value)
        {
            if (Done)
            {
                return false;
            }

            if (_index + value.Length > _raw.Length)
            {
                return false;
            }

            int i = _index;
            int j = 0;
            while (j < value.Length)
            {
                if (_raw[i++] != value[j++])
                {
                    return false;
                }
            }
            return true;
        }

        public void SkipWhitespace()
        {
            ReadUntil(ch => !Char.IsWhiteSpace(ch));
        }

        public void Skip()
        {
            Skip(1);
        }

        public bool Skip(string value)
        {
            if (!Peek(value))
            {
                return false;
            }

            Skip(value.Length);
            return true;
        }

        public bool Skip(char value)
        {
            if (Current != value)
            {
                return false;
            }

            Skip(1);
            return true;
        }

        public void Skip(int n)
        {
            _index = Math.Min(_raw.Length, _index + n);
        }

        public override string ToString()
        {
            return _raw.Substring(_index);
        }

        public string ReadToEnd()
        {
            if (Done)
            {
                return null;
            }

            string value = _raw.Substring(_index);
            _index = _raw.Length;
            return value;
        }

        public void PutBack(int n)
        {
            _index = Math.Max(0, _index - n);
        }
    }

    internal static class StringExtensions
    {
        public static IStringReader AsReader(this string value)
        {
            return new StringReader(value);
        }
    }
}
