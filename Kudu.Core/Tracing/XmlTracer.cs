using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Tracing
{
    // This writes traces for each request.  Each request will be traced in a different file.
    // The format is XML.  There is a MaxXmlFiles limit to keep the most recent traces.
    public class XmlTracer : ITracer
    {
        public const string IncomingRequestTrace = "Incoming Request";
        public const string OutgoingResponseTrace = "Outgoing response";
        public const string StartupRequestTrace = "Startup Request";
        public const string ProcessShutdownTrace = "Process Shutdown";
        public const string ExecutingExternalProcessTrace = "Executing external process";
        public const string BackgroundTrace = "BackgroundTrace";

        public const int MaxXmlFiles = 200;
        public const int CleanUpIntervalSecs = 10;

        private static object _cleanupLock = new object();
        private static bool _cleanupPending = false;
        private const string PendingXml = "_pending.xml";
        private static long _salt = 0;
        private static DateTime _lastCleanup = DateTime.MinValue;

        private readonly static TimeSpan ElapsedThreshold = TimeSpan.FromSeconds(10);
        private readonly static string[] TraceFilters = new[]
        {
            // portal polling
            "/api/webjobs",
            "/api/continuousjobs",
            "/api/triggeredjobs",
            "/api/siteextensions",
            "/api/processes",
            "/api/deployments",
            "/api/deployments/latest",
        };

        private Stack<TraceInfo> _infos;
        private string _path;
        private string _file;
        private int _statusCode;
        private bool _isStartElement;
        private TraceLevel _level;

        public XmlTracer(string path, TraceLevel level)
        {
            _path = path;
            _level = level;

            _infos = new Stack<TraceInfo>();
            _isStartElement = false;
        }

        public TraceLevel TraceLevel
        {
            get { return _level; }
        }

        public IDisposable Step(string title, IDictionary<string, string> attributes)
        {
            if (_level <= TraceLevel.Off)
            {
                return DisposableAction.Noop;
            }

            return WriteStartElement(title, attributes);
        }

        public void Trace(string value, IDictionary<string, string> attributes)
        {
            // Add a fake step
            using (Step(value, attributes)) { }
        }

        private IDisposable WriteStartElement(string title, IDictionary<string, string> attribs)
        {
            try
            {
                var info = new TraceInfo(title, attribs);

                var ensureMaxXmlFiles = false;
                if (String.IsNullOrEmpty(_file))
                {
                    ensureMaxXmlFiles = true;

                    // generate trace file name base on attribs
                    _file = GenerateFileName(info);
                }

                var strb = new StringBuilder();

                if (_isStartElement)
                {
                    strb.AppendLine(">");
                }

                strb.Append(new String(' ', _infos.Count * 2));
                strb.AppendFormat("<step title=\"{0}\" ", XmlUtility.EscapeXmlText(title));
                strb.AppendFormat("date=\"{0}\" ", info.StartTime.ToString("yyyy-MM-ddTHH:mm:ss.fff"));
                if (_infos.Count == 0)
                {
                    strb.AppendFormat("instance=\"{0}\" ", InstanceIdUtility.GetShortInstanceId());
                }

                foreach (var attrib in attribs)
                {
                    if (TraceExtensions.IsNonDisplayableAttribute(attrib.Key))
                    {
                        continue;
                    }

                    strb.AppendFormat("{0}=\"{1}\" ", attrib.Key, XmlUtility.EscapeXmlText(attrib.Value));
                }

                FileSystemHelpers.AppendAllTextToFile(_file, strb.ToString());
                _infos.Push(info);
                _isStartElement = true;

                if (ensureMaxXmlFiles)
                {
                    EnsureMaxXmlFiles();
                }

                return new DisposableAction(() => WriteEndTrace());
            }
            catch (Exception ex)
            {
                WriteUnexpectedException(ex);
            }

            return DisposableAction.Noop;
        }

        private void WriteEndTrace()
        {
            try
            {
                var info = _infos.Pop();
                var elapsed = DateTime.UtcNow - info.StartTime;
                var strb = new StringBuilder();
                if (_isStartElement)
                {
                    strb.Append("/>");
                }
                else
                {
                    strb.Append(new String(' ', _infos.Count * 2));
                    strb.Append("</step>");
                }

                strb.AppendLine(String.Format("<!-- duration: {0:0}ms -->", elapsed.TotalMilliseconds));

                FileSystemHelpers.AppendAllTextToFile(_file, strb.ToString());
                _isStartElement = false;

                // adjust filename with statusCode
                if (info.Title == XmlTracer.OutgoingResponseTrace && _file.EndsWith(PendingXml, StringComparison.OrdinalIgnoreCase))
                {
                    if (!Int32.TryParse(info.Attributes["statusCode"], out _statusCode))
                    {
                        _statusCode = 0;
                    }
                }

                if (_infos.Count == 0)
                {
                    // avoid spamming the traces
                    if (!ShouldTrace(_level, info, _statusCode))
                    {
                        FileSystemHelpers.DeleteFileSafe(_file);
                    }
                    else
                    {
                        var file = _file;
                        if (_file.EndsWith(PendingXml, StringComparison.OrdinalIgnoreCase))
                        {
                            file = file.Replace(PendingXml, _statusCode <= 0 ? ".xml" : String.Format("_{0}.xml", _statusCode));
                        }

                        file = file.Replace(".xml", String.Format("_{0:0}s.xml", elapsed.TotalSeconds));
                        FileSystemHelpers.MoveFile(_file, file);
                    }

                    _file = null;
                }
            }
            catch (Exception ex)
            {
                WriteUnexpectedException(ex);
            }
        }

        public static bool ShouldTrace(TraceLevel level, TraceInfo info, int statusCode)
        {
            // if SCM_TRACE_LEVEL is set to 4 (verbose), always trace.
            if (level >= TraceLevel.Verbose)
            {
                return true;
            }

            // for api taking long time (>10s), always trace.
            if (DateTime.UtcNow - info.StartTime >= ElapsedThreshold)
            {
                return true;
            }

            string type;
            string method;
            string path;
            if (!info.Attributes.TryGetValue("type", out type)
                || !info.Attributes.TryGetValue("method", out method)
                || !info.Attributes.TryGetValue("url", out path))
            {
                return true;
            }

            // trace non get request
            if (!String.Equals("request", type, StringComparison.OrdinalIgnoreCase)
                || !String.Equals("GET", method, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // NotModified
            if (statusCode == 304)
            {
                return false;
            }

            // non OK
            if (statusCode != 200)
            {
                return true;
            }

            // filter out those that may be polled by portal.
            // remove query string
            path = path.Split('?')[0].TrimEnd('/');
            return !TraceFilters.Any(filter => String.Equals(filter, path, StringComparison.OrdinalIgnoreCase));
        }

        private void EnsureMaxXmlFiles()
        {
            // _lastCleanup ensures we only check (iterate) the log directory every certain period.
            // _cleanupPending ensures there is one cleanup thread at a time.
            lock (_cleanupLock)
            {
                if (_cleanupPending)
                {
                    return;
                }

                var now = DateTime.UtcNow;
                if (_lastCleanup.AddSeconds(CleanUpIntervalSecs) > now)
                {
                    return;
                }

                _lastCleanup = now;
                _cleanupPending = true;
            }

            try
            {
                ITracer tracer = this;
                using (tracer.Step("Cleanup Xml Logs"))
                {
                    try
                    {
                        var files = FileSystemHelpers.GetFiles(_path, "*.xml");
                        if (files.Length < MaxXmlFiles)
                        {
                            return;
                        }

                        var toCleanup = files.Length - (MaxXmlFiles * 80) / 100;
                        var attribs = new Dictionary<string, string>
                        {
                            { "totalFiles", files.Length.ToString() },
                            { "totalCleanup", toCleanup.ToString() }
                        };
                        using (tracer.Step("Cleanup Infos", attribs))
                        {
                            foreach (var file in files.OrderBy(n => n).Take(toCleanup))
                            {
                                FileSystemHelpers.DeleteFileSafe(file);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        tracer.TraceError(ex);
                    }
                }
            }
            finally
            {
                _cleanupPending = false;
            }
        }

        // such as <datetime>_<instance>_<salt>_get_<url>_<statusCode>.xml
        // sample: 2014-11-17T04-59-21_d10e51_366_GET_api-deployments_200.xml
        private string GenerateFileName(TraceInfo info)
        {
            var strb = new StringBuilder();

            // add salt to avoid collision
            // mathematically improbable for salt to overflow 
            strb.AppendFormat("{0}_{1}_{2:000}",
                info.StartTime.ToString("yyyy-MM-ddTHH-mm-ss"),
                InstanceIdUtility.GetShortInstanceId(),
                Interlocked.Increment(ref _salt) % 1000);

            if (info.Title == XmlTracer.IncomingRequestTrace)
            {
                var path = info.Attributes["url"].Split('?')[0].Trim('/');
                strb.AppendFormat("_{0}_{1}", info.Attributes["method"], path.ToSafeFileName());
            }
            else if (info.Title == XmlTracer.StartupRequestTrace)
            {
                var path = info.Attributes["url"].Split('?')[0].Trim('/');
                strb.AppendFormat("_Startup_{0}_{1}", info.Attributes["method"], path.ToSafeFileName());
            }
            else if (info.Title == XmlTracer.ProcessShutdownTrace)
            {
                strb.Append("_Shutdown");
            }
            else if (info.Title == XmlTracer.ExecutingExternalProcessTrace)
            {
                var path = info.Attributes["path"].Split('\\').Last();
                strb.AppendFormat("_{0}", path);
            }
            else if (string.Equals(XmlTracer.BackgroundTrace, info.Title, StringComparison.Ordinal))
            {
                var path = info.Attributes["url"].Split('?')[0].Trim('/');
                strb.AppendFormat("_Background_{0}_{1}", info.Attributes["method"], path.ToSafeFileName());
            }
            else if (!String.IsNullOrEmpty(info.Title))
            {
                strb.AppendFormat("_{0}", info.Title.ToSafeFileName());
            }

            strb.Append(PendingXml);

            return Path.Combine(_path, strb.ToString());
        }

        private void WriteUnexpectedException(Exception ex)
        {
            try
            {
                var strb = new StringBuilder();
                strb.AppendFormat("{0}_{1}_{2:000}_UnexpectedException.xml",
                    DateTime.UtcNow.ToString("yyyy-MM-ddTHH-mm-ss"),
                    InstanceIdUtility.GetShortInstanceId(),
                    Interlocked.Increment(ref _salt) % 1000);

                FileSystemHelpers.AppendAllTextToFile(
                    Path.Combine(_path, strb.ToString()),
                    String.Format("<exception>{0}</exception>", XmlUtility.EscapeXmlText(ex.ToString())));
            }
            catch
            {
                // no-op
            }
        }
    }
}
