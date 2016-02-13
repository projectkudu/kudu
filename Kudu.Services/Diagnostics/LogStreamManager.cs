using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Web;
using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Core.Infrastructure;
using Kudu.Core.Settings;
using Kudu.Core.Tracing;
using Kudu.Services.Infrastructure;

using Environment = System.Environment;
using System.Diagnostics.CodeAnalysis;

namespace Kudu.Services.Performance
{
    public class LogStreamManager : IDisposable
    {
        private const string FilterQueryKey = "filter";
        private const string AzureDriveEnabledKey = "AzureDriveEnabled";

        // Azure 3 mins timeout, heartbeat every mins keep alive.
        private static string[] LogFileExtensions = new string[] { ".txt", ".log", ".htm" };
        private static TimeSpan HeartbeatInterval = TimeSpan.FromMinutes(1);

        private readonly object _thisLock = new object();
        private readonly string _logPath;
        private readonly IEnvironment _environment;
        private readonly ITracer _tracer;
        private readonly IOperationLock _operationLock;
        private readonly List<ProcessRequestAsyncResult> _results;

        private Dictionary<string, long> _logFiles;
        private FileSystemWatcher _watcher;
        private Timer _heartbeat;
        private DateTime _lastTraceTime = DateTime.UtcNow;
        private DateTime _startTime = DateTime.UtcNow;
        private TimeSpan _timeout;
        private string _filter;
        private bool _enableTrace;

        private ShutdownDetector _shutdownDetector;
        private CancellationTokenRegistration _cancellationTokenRegistration;

        public LogStreamManager(string logPath, 
                                IEnvironment environment,
                                IDeploymentSettingsManager settings, 
                                ITracer tracer, 
                                ShutdownDetector shutdownDetector,
                                IOperationLock operationLock)
        {
            _logPath = logPath;
            _tracer = tracer;
            _environment = environment;
            _shutdownDetector = shutdownDetector;
            _timeout = settings.GetLogStreamTimeout();
            _operationLock = operationLock;
            _results = new List<ProcessRequestAsyncResult>();
        }

        public IAsyncResult BeginProcessRequest(HttpContext context, AsyncCallback cb, object extraData)
        {
            // Close the client with a clear message when the app is shut down
            _cancellationTokenRegistration = _shutdownDetector.Token.Register(() =>
            {
                TerminateClient(String.Format(CultureInfo.CurrentCulture, Resources.LogStream_AppShutdown, Environment.NewLine, DateTime.UtcNow.ToString("s")));
            });

            string path = ParseRequest(context);
            if (!Directory.Exists(path))
            {
                throw new HttpException((Int32)HttpStatusCode.NotFound, string.Format("The directory name {0} does not exist.", path)); 
            }

            ProcessRequestAsyncResult result = new ProcessRequestAsyncResult(context, cb, extraData);

            WriteInitialMessage(context);

            lock (_thisLock)
            {
                _results.Add(result);

                Initialize(path);
            }

            // enable application diagnostic trace automatically if connecting to root or application path
            // it will be turn off automatically every 24 hours
            if (_enableTrace)
            {
                _operationLock.LockOperation(() =>
                {
                    var diagnostics = new DiagnosticsSettingsManager(Path.Combine(_environment.DiagnosticsPath, Constants.SettingsJsonFile), _tracer);
                    diagnostics.UpdateSetting(AzureDriveEnabledKey, true);
                }, TimeSpan.FromSeconds(30));
            }

            return result;
        }

        public void EndProcessRequest(IAsyncResult result)
        {
            ProcessRequestAsyncResult.End(result);

            _cancellationTokenRegistration.Dispose();
        }

        private void Initialize(string path)
        {
            System.Diagnostics.Debug.Assert(_watcher == null, "we only allow one manager per request!");

            if (_watcher == null)
            {
                FileSystemWatcher watcher = new FileSystemWatcher(path);
                watcher.Changed += new FileSystemEventHandler(DoSafeAction<object, FileSystemEventArgs>(OnChanged, "LogStreamManager.OnChanged"));
                watcher.Deleted += new FileSystemEventHandler(DoSafeAction<object, FileSystemEventArgs>(OnDeleted, "LogStreamManager.OnDeleted"));
                watcher.Renamed += new RenamedEventHandler(DoSafeAction<object, RenamedEventArgs>(OnRenamed, "LogStreamManager.OnRenamed"));
                watcher.Error += new ErrorEventHandler(DoSafeAction<object, ErrorEventArgs>(OnError, "LogStreamManager.OnError"));
                watcher.IncludeSubdirectories = true;
                watcher.EnableRaisingEvents = true;
                _watcher = watcher;
            }

            if (_heartbeat == null)
            {
                _heartbeat = new Timer(OnHeartbeat, null, HeartbeatInterval, HeartbeatInterval);
            }

            if (_logFiles == null)
            {
                var logFiles = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                foreach (var ext in LogFileExtensions)
                {
                    foreach (var file in Directory.GetFiles(path, "*" + ext, SearchOption.AllDirectories))
                    {
                        try
                        {
                            logFiles[file] = new FileInfo(file).Length;
                        }
                        catch (Exception ex)
                        {
                            // avoiding racy with providers cleaning up log file
                            _tracer.TraceError(ex);
                        }
                    }
                }

                _logFiles = logFiles;
            }
        }

        private void Reset()
        {
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                // dispose is blocked till all change request handled, 
                // this could lead to deadlock as we share the same lock
                // http://stackoverflow.com/questions/73128/filesystemwatcher-dispose-call-hangs
                // in the meantime, let GC handle it
                // _watcher.Dispose();
                _watcher = null;
            }

            if (_heartbeat != null)
            {
                _heartbeat.Dispose();
                _heartbeat = null;
            }

            _logFiles = null;
        }

        private static void WriteInitialMessage(HttpContext context)
        {
            context.Response.Write(String.Format(CultureInfo.CurrentCulture, Resources.LogStream_Welcome, DateTime.UtcNow.ToString("s"), Environment.NewLine));
        }

        private void OnHeartbeat(object state)
        {
            try
            {
                try
                {
                    TimeSpan ts = DateTime.UtcNow.Subtract(_startTime);
                    if (ts >= _timeout)
                    {
                        TerminateClient(String.Format(CultureInfo.CurrentCulture, Resources.LogStream_Timeout, DateTime.UtcNow.ToString("s"), (int)ts.TotalMinutes, Environment.NewLine));
                    }
                    else
                    {
                        ts = DateTime.UtcNow.Subtract(_lastTraceTime);
                        if (ts >= HeartbeatInterval)
                        {
                            NotifyClient(String.Format(CultureInfo.CurrentCulture, Resources.LogStream_Heartbeat, DateTime.UtcNow.ToString("s"), (int)ts.TotalMinutes, Environment.NewLine));
                        }
                    }
                }
                catch (Exception ex)
                {
                    using (_tracer.Step("LogStreamManager.OnHeartbeat"))
                    {
                        _tracer.TraceError(ex);
                    }
                }
            }
            catch
            {
                // no-op
            }
        }

        // Suppress exception on callback to not crash the process.
        private Action<T1, T2> DoSafeAction<T1, T2>(Action<T1, T2> func, string eventName)
        {
            return (t1, t2) =>
            {
                try
                {
                    try
                    {
                        func(t1, t2);
                    }
                    catch (Exception ex)
                    {
                        using (_tracer.Step(eventName))
                        {
                            _tracer.TraceError(ex);
                        }
                    }
                }
                catch
                {
                    // no-op
                }
            };
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType == WatcherChangeTypes.Changed && MatchFilters(e.FullPath))
            {
                // reading the delta of file changed, retry if failed.
                IEnumerable<string> lines = null;
                OperationManager.Attempt(() =>
                {
                    lines = GetChanges(e);
                }, 3, 100);

                if (lines.Count() > 0)
                {
                    _lastTraceTime = DateTime.UtcNow;

                    NotifyClient(lines);
                }
            }
        }

        private string ParseRequest(HttpContext context)
        {
            _filter = context.Request.QueryString[FilterQueryKey];

            // path route as in logstream/{*path} without query strings
            string routePath = context.Request.RequestContext.RouteData.Values["path"] as string;
            
            // trim '/'
            routePath = String.IsNullOrEmpty(routePath) ? routePath : routePath.Trim('/');

            // logstream at root
            if (String.IsNullOrEmpty(routePath))
            {
                _enableTrace = true;
                return _logPath;
            }

            // in case of application or http log, we ensure directory
            string firstPath = routePath.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries)[0];
            bool isApplication = String.Equals(firstPath, "Application", StringComparison.OrdinalIgnoreCase);
            if (isApplication)
            {
                _enableTrace = true;
                FileSystemHelpers.EnsureDirectory(Path.Combine(_logPath, firstPath));
            }
            else
            {
                bool isHttp = String.Equals(firstPath, "http", StringComparison.OrdinalIgnoreCase);
                if (isHttp)
                {
                    FileSystemHelpers.EnsureDirectory(Path.Combine(_logPath, firstPath));
                }
            }

            return Path.Combine(_logPath, routePath);
        }

        private static bool MatchFilters(string fileName)
        {
            if (!string.IsNullOrEmpty(fileName))
            {
                foreach (string ext in LogFileExtensions)
                {
                    if (fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void NotifyClient(string text)
        {
            NotifyClient(new string[] { text });
        }

        private void NotifyClient(IEnumerable<string> lines)
        {
            lock (_thisLock)
            {
                Lazy<List<ProcessRequestAsyncResult>> disconnects = new Lazy<List<ProcessRequestAsyncResult>>(() => new List<ProcessRequestAsyncResult>());
                foreach (ProcessRequestAsyncResult result in _results)
                {
                    if (result.HttpContext.Response.IsClientConnected)
                    {
                        try
                        {
                            foreach (var line in lines)
                            {
                                result.HttpContext.Response.Write(line);
                            }
                        }
                        catch (Exception)
                        {
                            disconnects.Value.Add(result);
                        }
                    }
                    else
                    {
                        disconnects.Value.Add(result);
                    }
                }

                if (disconnects.IsValueCreated)
                {
                    foreach (ProcessRequestAsyncResult result in disconnects.Value)
                    {
                        _results.Remove(result);
                        result.Complete(false);
                    }

                    if (_results.Count == 0)
                    {
                        _logFiles.Clear();
                        Reset();
                    }
                }
            }
        }

        private IEnumerable<string> GetChanges(FileSystemEventArgs e)
        {
            lock (_thisLock)
            {
                // do no-op if races between idle timeout and file change event
                if (_results.Count == 0)
                {
                    return Enumerable.Empty<string>();
                }

                long offset = 0;
                if (!_logFiles.TryGetValue(e.FullPath, out offset))
                {
                    _logFiles[e.FullPath] = 0;
                }

                using (FileStream fs = new FileStream(e.FullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    long length = fs.Length;

                    // file was truncated
                    if (offset > length)
                    {
                        _logFiles[e.FullPath] = offset = 0;
                    }

                    // multiple events
                    if (offset == length)
                    {
                        return Enumerable.Empty<string>();
                    }

                    if (offset != 0)
                    {
                        fs.Seek(offset, SeekOrigin.Begin);
                    }

                    List<string> changes = new List<string>();

                    StreamReader reader = new StreamReader(fs);
                    while (!reader.EndOfStream)
                    {
                        string line = ReadLine(reader);
                        if (String.IsNullOrEmpty(_filter) || line.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            changes.Add(line);
                        }

                        offset += line.Length;
                    }

                    // Adjust offset and return changes
                    _logFiles[e.FullPath] = offset;

                    return changes;
                }
            }
        }

        private void OnDeleted(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType == WatcherChangeTypes.Deleted)
            {
                lock (_thisLock)
                {
                    _logFiles.Remove(e.FullPath);
                }
            }
        }

        private void OnRenamed(object sender, RenamedEventArgs e)
        {
            if (e.ChangeType == WatcherChangeTypes.Renamed)
            {
                lock (_thisLock)
                {
                    _logFiles.Remove(e.OldFullPath);
                }
            }
        }

        private void OnError(object sender, ErrorEventArgs e)
        {
            using (_tracer.Step("FileSystemWatcher.OnError"))
            {
                _tracer.TraceError(e.GetException());
            }

            try
            {
                lock (_thisLock)
                {
                    if (_watcher != null)
                    {
                        string path = _watcher.Path;
                        Reset();
                        Initialize(path);
                    }
                }
            }
            catch (Exception ex)
            {
                OnCriticalError(ex);
            }
        }

        private void OnCriticalError(Exception ex)
        {
            TerminateClient(String.Format(CultureInfo.CurrentCulture, Resources.LogStream_Error, Environment.NewLine, DateTime.UtcNow.ToString("s"), ex.Message));
        }

        private void TerminateClient(string text)
        {
            NotifyClient(text);

            lock (_thisLock)
            {
                foreach (ProcessRequestAsyncResult result in _results)
                {
                    result.Complete(false);
                }

                _results.Clear();

                // Proactively cleanup resources
                Reset();
            }
        }

        // this has the same performance and implementation as StreamReader.ReadLine()
        // they both account for '\n' or '\r\n' as new line chars.  the difference is 
        // this returns the result with preserved new line chars.
        // without this, logstream can only guess whether it is '\n' or '\r\n' which is 
        // subjective to each log providers/files.
        private static string ReadLine(StreamReader reader)
        {
            var strb = new StringBuilder();
            int val;
            while ((val = reader.Read()) >= 0)
            {
                char ch = (char)val;
                strb.Append(ch);
                switch (ch)
                {
                    case '\r':
                    case '\n':
                        if (ch == '\r' && (char)reader.Peek() == '\n')
                        {
                            ch = (char)reader.Read();
                            strb.Append(ch);
                        }
                        return strb.ToString();
                    default:
                        break;
                }
            }

            return strb.ToString();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                Reset();
            }
        }

        class ProcessRequestAsyncResult : IAsyncResult
        {
            private HttpContext _context;
            private AsyncCallback _callback;
            private object _state;
            private ManualResetEvent _waitHandle;
            private bool _completedSynchronously;
            private bool _completed;

            public ProcessRequestAsyncResult(HttpContext context, AsyncCallback callback, object state)
            {
                _context = context;
                _callback = callback;
                _state = state;

                _context.Response.Buffer = false;
                _context.Response.BufferOutput = false;
                _context.Response.ContentType = _context.Request.Headers["FunctionsPortal"] != null
                    ? "custom-functions/stream"
                    : "text/plain";
                _context.Response.StatusCode = 200;
            }

            [SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "result", Justification = "By design")]
            public static void End(IAsyncResult result)
            {
                // no-op
            }

            public void Complete(bool completedSynchronously)
            {
                _completed = true;
                _completedSynchronously = completedSynchronously;

                if (_waitHandle != null)
                {
                    _waitHandle.Set();
                }

                if (_callback != null)
                {
                    _callback(this);
                }
            }

            public HttpContext HttpContext
            {
                get { return _context; }
            }

            public object AsyncState
            {
                get { return _state; }
            }

            public WaitHandle AsyncWaitHandle
            {
                get { return _waitHandle ?? (_waitHandle = new ManualResetEvent(false)); }
            }

            public bool CompletedSynchronously
            {
                get { return _completedSynchronously; }
            }

            public bool IsCompleted
            {
                get { return _completed; }
            }
        }
    }
}
