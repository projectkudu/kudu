using System;
using System.Text;

namespace Kudu.Core.Infrastructure {
    internal class BetterStringReader {
        private string _raw;
        private int _index;

        public BetterStringReader(string raw) {
            _raw = raw;
        }

        private bool Done {
            get {
                return _index >= _raw.Length;
            }
        }

        private char Current {
            get {
                return Done ? '\0' : _raw[_index];
            }
        }

        public string ReadLine() {
            string value = ReadUntil('\n');
            var ch = Read();
            if (String.IsNullOrEmpty(value) && ch == '\0') {
                return null;
            }
            return value + ch;
        }

        private char Read() {
            char ch = Current;
            Skip();
            return ch;
        }

        public int? ReadInt() {
            string value = ReadUntil(ch => !Char.IsDigit(ch));
            int val;
            if (Int32.TryParse(value, out val)) {
                return val;
            }
            return null;
        }

        public string ReadUntilWhitespace() {
            return ReadUntil(ch => Char.IsWhiteSpace(ch));
        }

        public string ReadUntil(char delimiter) {
            string result;
            TryReadUntil(delimiter, out result);
            return result;
        }

        public string ReadUntil(string value) {
            string result;
            TryReadUntil(value, out result);
            return result;
        }

        public string ReadUntil(Func<char, bool> condition) {
            string result;
            TryReadUntil(condition, out result);
            return result;
        }

        public bool TryReadUntil(Func<char, bool> condition, out string result) {
            var sb = new StringBuilder();
            while (!Done) {
                if (condition(Current)) {
                    result = sb.ToString();
                    return true;
                }
                sb.Append(Current);
                Skip();
            }
            result = sb.ToString();
            if (String.IsNullOrEmpty(result)) {
                result = null;
            }
            return false;
        }

        public bool TryReadUntil(char delimiter, out string result) {
            return TryReadUntil(ch => ch == delimiter, out result);
        }

        public bool TryReadUntil(string value, out string result) {
            var sb = new StringBuilder();
            result = null;
            while (!Done) {
                if (Peek(value)) {
                    result = sb.ToString();
                    return true;
                }
                sb.Append(Current);
                Skip();
            }
            result = sb.ToString();
            if (String.IsNullOrEmpty(result)) {
                result = null;
            }
            return false;
        }

        public bool Peek(string value) {
            // REVIEW: This isn't fast but it doesn't need to be right now
            return !Done && _raw.Substring(_index).StartsWith(value);
        }

        public void SkipWhitespace() {
            ReadUntil(ch => !Char.IsWhiteSpace(ch));
        }

        public void Skip() {
            Skip(1);
        }

        public void Skip(int n) {
            _index += n;
        }

        public override string ToString() {
            return _raw.Substring(_index);
        }
    }

    internal static class BetterStringReaderExtensions {
        public static BetterStringReader AsReader(this string value) {
            return new BetterStringReader(value);
        }
    }
}
