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
        private readonly TraceLevel _level;
        private int _depth;

        public TextTracer(string path, TraceLevel level, int depth = 0)
        {
            _logFile = new LogFileHelper(this, path);
            _level = level;
            _depth = depth;

            // Initialize cleanup timer
            OperationManager.SafeExecute(() => CleanupHelper.Initialize(path));
        }

        public TraceLevel TraceLevel
        {
            get { return _level; }
        }

        public IDisposable Step(string title, IDictionary<string, string> attributes)
        {
            try
            {
                _logFile.WriteLine(title, attributes, _depth);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return DisposableAction.Noop;
            }

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
            try
            {
                _logFile.WriteLine(message, attributes, _depth);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        private class LogFileHelper
        {
            private readonly ITracer _tracer;
            private readonly string _logFile;
            private Stopwatch _stopWatch;

            public LogFileHelper(ITracer tracer, string logFile)
            {
                _tracer = tracer;
                _logFile = logFile;
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

                if (!_tracer.ShouldTrace(attributes))
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
                            if (TraceExtensions.IsNonDisplayableAttribute(pair.Key)) continue;

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

                using (StreamWriter writer = new StreamWriter(FileSystemHelpers.OpenFile(_logFile, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)))
                {
                    writer.WriteLine(strb.ToString());
                }
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
            private const int FileStaleMinutes = 5; // 5min

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
                            foreach (FileInfoBase child in parent.GetFiles("*.txt", SearchOption.AllDirectories))
                            {
                                _files[child.FullName] = child.LastWriteTimeUtc.AddMinutes(FileStaleMinutes);
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
                    _files[path] = DateTime.UtcNow.AddMilliseconds(TimerInterval);

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

