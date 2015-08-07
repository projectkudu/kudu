using Kudu.Core.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Kudu.Core.Deployment
{
    public class StructuredTextDocument<T>
    {
        private const string LogTemplate = "{0}{1}";
        private readonly string _path;
        private readonly Func<T, string> _serializer;
        private readonly Func<string, T> _deserializer;

        public StructuredTextDocument(string path, Func<T, string> serializer, Func<string, T> deserializer)
        {
            _path = path;
            _serializer = serializer;
            _deserializer = deserializer;
        }

        public IEnumerable<StructuredTextDocumentEntry<T>> GetDocument()
        {
            return ParseLines(FileSystemHelpers.ReadAllLines(_path), startIndex: 0, depth: 0);
        }

        /// <summary>
        /// Write uses the serializer that was specified in the constructor to convert T into a string
        /// then logs it with the correct depth.
        /// The depth determines how many \t are there in front of the log message. Which implies the
        /// Parent/Child relationship between the log entries.
        /// Here is an example of how the log looks like with 1 level of depth
        /// 0000-00-00T00:00:00.0000000Z,Message 1,00000000-0000-0000-0000-000000000000,0
        /// 0000-00-00T00:00:00.0000000Z,Message 2,00000000-0000-0000-0000-000000000000,0
        /// \t0000-00-00T00:00:00.0000000Z,Message 2.1,,0
        /// \t0000-00-00T00:00:00.0000000Z,Message 2.2,,0
        /// 0000-00-00T00:00:00.0000000Z,Message 3,00000000-0000-0000-0000-000000000000,0
        /// </summary>
        /// <param name="value">Object to be serialized and logged in the StructuredTextDocument</param>
        /// <param name="depth">Determines the Parent/Child relationship between log entries. depth = 0 is parent for depth = 1</param>
        public void Write(T value, int depth)
        {
            var stringValue = _serializer(value);
            ValidateStringMessage(stringValue);

            var stringDepth = string.Concat(Enumerable.Range(0, depth).Select(_ => StructuredTextDocument.LeadingCharacter));
            var logEntry = string.Format(LogTemplate, stringDepth, stringValue);
            Log(logEntry);
        }

        private IEnumerable<StructuredTextDocumentEntry<T>> ParseLines(string[] lines, int startIndex, int depth)
        {
            List<StructuredTextDocumentEntry<T>> collection = new List<StructuredTextDocumentEntry<T>>();
            StructuredTextDocumentEntry<T> logEntry = null;
            for (var i = startIndex; i < lines.Length; i++)
            {
                //skip empty lines
                if (string.IsNullOrWhiteSpace(lines[i]))
                    continue;

                var entryDepth = StructuredTextDocument.GetEntryDepth(lines[i]);
                if (entryDepth == depth)
                {
                    logEntry = new StructuredTextDocumentEntry<T>
                    {
                        LogEntry = _deserializer(lines[i].Substring(depth)),
                        Children = Enumerable.Empty<StructuredTextDocumentEntry<T>>()
                    };
                    collection.Add(logEntry);
                }
                else if (entryDepth > depth)
                {
                    if (logEntry == null)
                    {
                        throw new FormatException(string.Format("log file '{0}' is in an invalid format", _path));
                    }

                    logEntry.Children = ParseLines(lines, i, entryDepth);
                    i += (logEntry.Children.Count() - 1);
                }
                else if (entryDepth < depth)
                {
                    return collection;
                }
            }
            return collection;
        }

        private void Log(string logEntry)
        {
            FileSystemHelpers.AppendAllTextToFile(_path, string.Concat(logEntry, System.Environment.NewLine));
        }

        private static void ValidateStringMessage(string message)
        {
            if (message.StartsWith(StructuredTextDocument.LeadingCharacter, StringComparison.OrdinalIgnoreCase))
            {
                throw new FormatException(
                    string.Format("Serialized log of type '{0}' => '{1}' can't start with character '{2}'",
                    typeof(T).Name,
                    message,
                    StructuredTextDocument.PrintableLeadingCharacter));
            }

            foreach (var pair in StructuredTextDocument.NotAllowedSequences)
            {
                if (message.IndexOf(pair.Key, StringComparison.OrdinalIgnoreCase) != -1)
                {
                    throw new FormatException(
                        string.Format("Serialized log of type '{0}' => '{1}' contains an invalid sequance '{2}' for a logEntry in a StructuredTextDocument",
                        typeof(T).Name,
                        message,
                        pair.Value));
                }
            }
        }
    }

    public static class StructuredTextDocument
    {
        public const string LeadingCharacter = "\t";
        public const string PrintableLeadingCharacter = "\\t";
        public static readonly IDictionary<string, string> NotAllowedSequences = new Dictionary<string, string> {  { "\r\n", "\\r\\n" }, { "\r", "\\r" }, { "\n", "\\n" }};
        public static int GetEntryDepth(string entry)
        {
            return entry.TakeWhile(c => LeadingCharacter.Equals(c.ToString())).Count();
        }
    }
}
