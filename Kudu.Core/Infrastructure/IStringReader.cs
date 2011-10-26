using System;

namespace Kudu.Core.Infrastructure
{
    internal interface IStringReader
    {
        bool Done { get; }
        bool Peek(string value);
        char Read();
        string Read(int n);
        int ReadInt();
        string ReadLine();
        string ReadUntil(char delimiter);
        string ReadUntil(Func<char, bool> condition);
        string ReadUntil(string value);
        string ReadUntilWhitespace();
        string ReadToEnd();
        void Skip();
        void Skip(int n);
        bool Skip(string value);
        bool Skip(char value);
        void SkipWhitespace();
        bool TryReadUntil(char delimiter, out string result);
        bool TryReadUntil(Func<char, bool> condition, out string result);
        bool TryReadUntil(string value, out string result);
        void PutBack(int n);
    }
}
