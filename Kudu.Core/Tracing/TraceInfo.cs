using System;
using System.Collections.Generic;

namespace Kudu.Core.Tracing
{
    public class TraceInfo
    {
        private string _title;
        private IDictionary<string, string> _attribs;
        private DateTime _startTime;

        public TraceInfo(string title, IDictionary<string, string> attribs)
        {
            _title = title;
            _attribs = attribs;
            _startTime = DateTime.UtcNow;
        }

        public string Title
        {
            get { return _title; }
        }

        public IDictionary<string, string> Attributes
        {
            get { return _attribs; }
        }

        public DateTime StartTime
        {
            get { return _startTime; }
        }
    }
}
