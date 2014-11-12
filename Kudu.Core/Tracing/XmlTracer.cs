using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
        
        public const int MaxXmlFiles = 200;
        public const int CleanUpIntervalSecs = 10;

        private static List<KeyValuePair<string, string>> SpecialChars = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("&", "&amp;"),
            new KeyValuePair<string, string>("<", "&lt;"),
            new KeyValuePair<string, string>(">", "&gt;"),
            new KeyValuePair<string, string>("\"", "&quot;"),
            new KeyValuePair<string, string>("'", "&apos;"),
        };

        private const string PendingXml = "_pending.xml";
        private static long _salt = 0;
        private static DateTime _lastCleanup = DateTime.MinValue;

        private Stack<TraceInfo> _infos;
        private string _path;
        private string _file;
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

                if (String.IsNullOrEmpty(_file))
                {
                    EnsureMaxXmlFiles();

                    // generate trace file name base on attribs
                    _file = GenerateFileName(info);
                }

                var strb = new StringBuilder();

                if (_isStartElement)
                {
                    strb.AppendLine(">");
                }

                strb.Append(new String(' ', _infos.Count * 2));
                strb.AppendFormat("<step title=\"{0}\" ", EncodeXml(title));
                strb.AppendFormat("date=\"{0}\" ", DateTime.UtcNow.ToString("yyy-MM-ddTHH:mm:ss.fff"));
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

                    strb.AppendFormat("{0}=\"{1}\" ", attrib.Key, EncodeXml(attrib.Value));
                }

                FileSystemHelpers.AppendAllTextToFile(_file, strb.ToString());
                _infos.Push(info);
                _isStartElement = true;

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
                var strb = new StringBuilder();
                if (_isStartElement)
                {
                    strb.AppendLine("/>");
                }
                else
                {
                    strb.Append(new String(' ', _infos.Count * 2));
                    strb.AppendLine("</step>");
                }

                FileSystemHelpers.AppendAllTextToFile(_file, strb.ToString());
                _isStartElement = false;

                // adjust filename with statusCode
                if (info.Title == XmlTracer.OutgoingResponseTrace && _file.EndsWith(PendingXml, StringComparison.OrdinalIgnoreCase))
                {
                    var file = _file.Replace(PendingXml, String.Format("_{0}.xml", info.Attributes["statusCode"]));
                    FileSystemHelpers.MoveFile(_file, file);
                    _file = file;
                }

                if (_infos.Count == 0)
                {
                    // Suppress traces with NotModified statusCode to avoid client polling in nature
                    if (_file.EndsWith("_304.xml", StringComparison.OrdinalIgnoreCase) && _level != System.Diagnostics.TraceLevel.Verbose)
                    {
                        FileSystemHelpers.DeleteFileSafe(_file);
                    }
                    else if (_file.EndsWith(PendingXml, StringComparison.OrdinalIgnoreCase))
                    {
                        var file = _file.Replace(PendingXml, ".xml");
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

        private void EnsureMaxXmlFiles()
        {
            var now = DateTime.UtcNow;
            if (_lastCleanup.AddSeconds(CleanUpIntervalSecs) > now)
            {
                return;
            }

            _lastCleanup = now;

            try
            {
                var files = FileSystemHelpers.GetFiles(_path, "*.xml");
                if (files.Length < MaxXmlFiles)
                {
                    return;
                }

                foreach (var file in files.OrderBy(n => n).Take(files.Length - (MaxXmlFiles * 80)/100))
                {
                    FileSystemHelpers.DeleteFileSafe(file);
                }

                // delete old style trace.xml
                FileSystemHelpers.DeleteFileSafe(Path.Combine(_path, "trace.xml"));
            }
            catch
            {
                // no-op
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
                DateTime.UtcNow.ToString("yyyy-MM-ddTHH-mm-ss"),
                InstanceIdUtility.GetShortInstanceId(),
                Interlocked.Increment(ref _salt) % 1000);

            if (info.Title == XmlTracer.IncomingRequestTrace)
            {
                var path = info.Attributes["url"].Split('?')[0].Trim('/');
                strb.AppendFormat("_{0}_{1}", info.Attributes["method"], path.Replace('/', '-'));
            }
            else if (info.Title == XmlTracer.StartupRequestTrace)
            {
                var path = info.Attributes["url"].Split('?')[0].Trim('/');
                strb.AppendFormat("_Startup_{0}_{1}", info.Attributes["method"], path.Replace('/', '-'));
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
            else if (!String.IsNullOrEmpty(info.Title))
            {
                strb.AppendFormat("_{0}", info.Title.Replace(' ', '-'));
            }

            strb.Append(PendingXml);

            return Path.Combine(_path, strb.ToString());
        }

        // http://weblogs.sqlteam.com/mladenp/archive/2008/10/21/Different-ways-how-to-escape-an-XML-string-in-C.aspx
        private static string EncodeXml(string value)
        {
            string result = value;
            foreach (var pair in SpecialChars)
            {
                if (result.Contains(pair.Key))
                {
                    result = result.Replace(pair.Key, pair.Value);
                }
            }
            return result;
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
                    String.Format("<exception>{0}</exception>", EncodeXml(ex.ToString())));
            }
            catch
            {
                // no-op
            }
        }

        public class TraceInfo
        {
            private string _title;
            private IDictionary<string, string> _attribs;

            public TraceInfo(string title, IDictionary<string, string> attribs)
            {
                _title = title;
                _attribs = attribs;
            }

            public string Title 
            { 
                get { return _title; } 
            }

            public IDictionary<string, string> Attributes 
            {
                get { return _attribs; } 
            }
        }
    }
}
