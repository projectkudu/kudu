using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Timers;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Tracing
{
    public class TextTracer : ITracer
    {
        private readonly LogFileHelper _logFile;
        private int _depth;

        public TextTracer(string path, TraceLevel level)
            : this(new FileSystem(), path, level)
        {
        }

        public TextTracer(IFileSystem fileSystem, string path, TraceLevel level, int depth = 0)
        {
            _logFile = new LogFileHelper(fileSystem, path, level);
            _depth = depth;

            // Initialize cleanup timer
            CleanupHelper.Initialize(path);
        }

        public TraceLevel TraceLevel
        {
            get { return _logFile.TraceLevel; }
        }

        public IDisposable Step(string title, IDictionary<string, string> attributes)
        {
            _logFile.WriteLine(title, attributes, _depth);
            ++_depth;

            return new DisposableAction(() =>
            {
                try
                {
                    --_depth;
                    _logFile.WriteLine(title, attributes, _depth, true);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                }
            });
        }

        public void Trace(string message, IDictionary<string, string> attributes)
        {
            _logFile.WriteLine(message, attributes, _depth);
        }

        private class LogFileHelper
        {
            private readonly IFileSystem _fileSystem;
            private readonly string _logFile;
            private readonly TraceLevel _level;
            private Stopwatch _stopWatch;

            public LogFileHelper(IFileSystem fileSystem, string logFile, TraceLevel level)
            {
                _fileSystem = fileSystem;
                _logFile = logFile;
                _level = level;
            }

            public TraceLevel TraceLevel
            {
                get { return _level; }
            }

            public void WriteLine(string value, IDictionary<string, string> attributes, int depth, bool end = false)
            {
                string type;
                attributes.TryGetValue("type", out type);

                if (type == "request" && end)
                {
                    // add for delay cleanup
                    CleanupHelper.AddFile(_logFile);
                }

                if (_level < GetTraceLevel(type, attributes))
                {
                    return;
                }

                if (FilterTrace(attributes, out attributes))
                {
                    return;
                }

                var strb = new System.Text.StringBuilder();
                strb.Append(DateTime.UtcNow.ToString("s"));
                strb.Append(GetIndentation(depth + 1));

                if (end)
                {
                    strb.Append("Done ");
                }

                if (type == "process")
                {
                    strb.Append(attributes["path"]);
                    if (!end)
                    {
                        strb.Append(GetIndentation(1));
                        strb.Append(attributes["arguments"]);
                    }
                }
                else
                {
                    strb.Append(value);

                    if (!end)
                    {
                        foreach (KeyValuePair<string, string> pair in attributes)
                        {
                            strb.AppendFormat(", {0}: {1}", pair.Key, pair.Value);
                        }
                    }

                    if (type == "request")
                    {
                        if (!end)
                        {
                            _stopWatch = Stopwatch.StartNew();
                        }
                        else
                        {
                            _stopWatch.Stop();
                            strb.AppendFormat(", elapsed: {0} ms", _stopWatch.ElapsedMilliseconds);
                            strb.AppendLine();
                        }
                    }
                }

                using (StreamWriter writer = new StreamWriter(_fileSystem.File.Open(_logFile, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)))
                {
                    writer.WriteLine(strb.ToString());
                }
            }

            private static TraceLevel GetTraceLevel(string type, IDictionary<string, string> attributes)
            {
                if (IsError(type))
                {
                    return TraceLevel.Error;
                }
                else if (IsInfo(attributes))
                {
                    return TraceLevel.Info;
                }
                else
                {
                    return TraceLevel.Verbose;
                }
            }

            private static bool IsError(string type)
            {
                return type == "error";
            }

            // we don't include "error" in info as caller must be checking for that already
            private static bool IsInfo(IDictionary<string, string> attributes)
            {
                string value;
                if (attributes.TryGetValue("traceLevel", out value))
                {
                    return Int32.Parse(value) <= (int)TraceLevel.Info;
                }

                return false;
            }

            private static string GetIndentation(int count)
            {
                return new String(' ', count * 2);
            }

            // Known filtered traces
            private static bool FilterTrace(IDictionary<string, string> attributes, out IDictionary<string, string> filters)
            {
                filters = attributes;
                string type;
                if (attributes.TryGetValue("type", out type))
                {
                    if (type == "processOutput")
                    {
                        return attributes["exitCode"] == "0";
                    }
                    else if (type == "lock")
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        private static class CleanupHelper
        {
            private const int TimerInterval = 10000; // 10sec
            private const int FileStaleMinutes = 60; // 60min

            private static object _lock = new object();
            private static bool _init = false;
            private static Timer _timer;
            private static Dictionary<string, DateTime> _files = new Dictionary<string, DateTime>();

            public static void Initialize(string path)
            {
                if (_init)
                {
                    return;
                }

                lock (_lock)
                {
                    if (!_init)
                    {
                        var parent = new DirectoryInfo(Path.Combine(path, @"..\.."));
                        if (parent.Exists)
                        {
                            var utcNow = DateTime.UtcNow;
                            foreach (FileInfoBase child in parent.GetFiles("*.txt", SearchOption.AllDirectories))
                            {
                                if (utcNow.Subtract(child.LastWriteTimeUtc).TotalMinutes >= FileStaleMinutes)
                                {
                                    _files.Add(child.FullName, child.LastWriteTimeUtc);
                                }
                            }

                            EnsureTimer();
                        }

                        _init = true;
                    }
                }
            }

            public static void AddFile(string path)
            {
                lock (_lock)
                {
                    _files.Add(path, DateTime.UtcNow.AddMilliseconds(TimerInterval));

                    EnsureTimer();
                }
            }


            // caller must call under lock
            private static void EnsureTimer()
            {
                if (_files.Count > 0)
                {
                    if (_timer == null)
                    {
                        _timer = new Timer(TimerInterval);
                        _timer.Elapsed += OnTimedEvent;
                    }

                    if (!_timer.Enabled)
                    {
                        _timer.Enabled = true;
                    }
                }
            }

            private static void OnTimedEvent(object source, ElapsedEventArgs e)
            {
                lock (_lock)
                {
                    if (_files.Count > 0)
                    {
                        List<string> deleted = new List<string>();
                        DateTime utcNow = DateTime.UtcNow;
                        foreach (KeyValuePair<string, DateTime> pair in _files)
                        {
                            if (utcNow >= pair.Value)
                            {
                                try
                                {
                                    var file = new FileInfo(pair.Key);
                                    var parent = new DirectoryInfo(Path.Combine(pair.Key, @"..\.."));
                                    foreach (FileInfoBase child in parent.GetFiles(file.Name, SearchOption.AllDirectories))
                                    {
                                        FileSystemHelpers.DeleteFileSafe(child.FullName);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine(ex);
                                }

                                deleted.Add(pair.Key);
                            }
                        }

                        foreach (string file in deleted)
                        {
                            _files.Remove(file);
                        }
                    }

                    if (_files.Count == 0)
                    {
                        _timer.Enabled = false;
                    }
                }
            }
        }
    }
}

