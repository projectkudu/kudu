using Kudu.Core.Tracing;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Kudu.Core.Deployment
{
    public class StructuredTextLogger : IDetailedLogger
    {
        private readonly static object DocumentLock = new object();
        private readonly IAnalytics _analytics;
        private int _depth;
        private readonly string _path;
        private const string LogEntrySeparator = ",";
        private readonly StructuredTextDocument<LogEntry> _structuredTextDocument;

        private static IEnumerable<KeyValuePair<string, string>> EscapeChars = new Dictionary<string, string>
        {
            { ",", "&comma;" }
        };

        public StructuredTextLogger(string path, IAnalytics analytics)
        {
            _depth = 0;
            _path = path;
            _analytics = analytics;
            _structuredTextDocument = new StructuredTextDocument<LogEntry>(path,
                // DateTime.ToString("o") => "2015-08-04T00:08:38.5489308Z"
                e => string.Join(LogEntrySeparator, e.LogTime.ToString("o"), e.Message, e.Id, (int)e.Type),
                str =>
                {
                    var splitted = str.Split(new[] { LogEntrySeparator }, StringSplitOptions.None);
                    if (splitted.Length == 4)
                    {
                        var time = DateTime.Parse(splitted[0]).ToUniversalTime();
                        var message = UnsanitizeValue(splitted[1]);
                        var id = splitted[2];
                        var type = (LogEntryType)Int32.Parse(splitted[3]);
                        return new LogEntry(time, id, message, type);
                    }
                    else
                    {
                        throw new FormatException(string.Format("the log line \"{0}\" is in an invalid format", str));
                    }
                });
        }

        public ILogger Log(string value, LogEntryType type)
        {
            try
            {
                value = SanitizeValue(value);
                lock(DocumentLock)
                {
                    var logId = _depth == 0 ? Guid.NewGuid().ToString() : string.Empty;
                    _structuredTextDocument.Write(new LogEntry(DateTime.UtcNow, logId, value, type), _depth);
                }

                return new StructuredTextLogger(_path, _analytics)
                {
                    _depth = _depth + 1
                };
            }
            catch(Exception e)
            {
                _analytics.UnexpectedException(e);
                throw;
            }
        }

        public IEnumerable<LogEntry> GetLogEntries()
        {
            try
            {
                return _structuredTextDocument.GetDocument().Select(e => e.LogEntry);
            }
            catch(Exception e)
            {
                _analytics.UnexpectedException(e);
                throw;
            }
        }

        public IEnumerable<LogEntry> GetLogEntryDetails(string entryId)
        {
            try
            {
                var entry = _structuredTextDocument.GetDocument().FirstOrDefault(s => s.LogEntry.Id.Equals(entryId, StringComparison.OrdinalIgnoreCase));
                return entry == null
                    ? Enumerable.Empty<LogEntry>()
                    : entry.Children.Select(e => e.LogEntry);
            }
            catch(Exception e)
            {
                _analytics.UnexpectedException(e);
                throw;
            }
        }

        private static string SanitizeValue(string value)
        {
            foreach (var pair in EscapeChars.Union(StructuredTextDocument.NotAllowedSequences))
            {
                if (value.Contains(pair.Key))
                {
                    value = value.Replace(pair.Key, pair.Value);
                }
            }

            return value;
        }

        private static string UnsanitizeValue(string value)
        {
            foreach (var pair in EscapeChars)
            {
                if (value.Contains(pair.Value))
                {
                    value = value.Replace(pair.Value, pair.Key);
                }
            }

            return value;
        }
    }
}
