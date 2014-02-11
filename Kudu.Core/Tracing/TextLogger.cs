using System;
using System.IO;
using System.IO.Abstractions;
using Kudu.Core.Deployment;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Tracing
{
    public class TextLogger : ILogger
    {
        private readonly LogFileHelper _logFile;
        private readonly int _depth;

        public TextLogger(string path)
            : this(new LogFileHelper(path), 0)
        {
        }

        private TextLogger(LogFileHelper logFile, int depth)
        {
            _logFile = logFile;
            _depth = depth;
        }

        public ILogger Log(string value, LogEntryType type)
        {
            _logFile.WriteLine(value, _depth);
            return new TextLogger(_logFile, _depth + 1);
        }

        private class LogFileHelper
        {
            private readonly string _logFile;

            public LogFileHelper(string logFile)
            {
                _logFile = logFile;
            }

            public void WriteLine(string value, int depth)
            {
                var strb = new System.Text.StringBuilder();
                strb.Append(DateTime.UtcNow.ToString("s"));
                strb.Append(GetIndentation(depth + 1));
                strb.Append(value);

                using (StreamWriter writer = new StreamWriter(FileSystemHelpers.OpenFile(_logFile, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)))
                {
                    writer.WriteLine(strb.ToString());
                }
            }

            private static string GetIndentation(int count)
            {
                return new String(' ', count * 2);
            }
        }
    }
}
