using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kudu.Core.AnalyticsParser
{

    public class HTTPLog : Log, IComparable
    {
        private DateTime _date;
        private string _time;
        private string _statusCode;
        private string _clientServerURI;
        private string _timeTaken;
        private string _typeRequest;

        public override DateTime Date
        {
            get
            {
                return _date;
            }
            set
            {
                _date = value;
            }
        }

        public override string Time
        {
            get
            {
                return _time;
            }
            set
            {
                _time = value;
            }
        }

        public override string StatusCode
        {
            get
            {
                return _statusCode;
            }
            set
            {
                _statusCode = value;
            }
        }

        public override string URIRequested
        {
            get
            {
                return _clientServerURI;
            }
            set
            {
                _clientServerURI = value;
            }
        }

        public override string TimeTaken
        {
            get
            {
                return _timeTaken;
            }
            set
            {
                _timeTaken = value;
            }
        }

        public override string TypeRequest
        {
            get
            {
                return _typeRequest;
            }
            set
            {
                _typeRequest = value;
            }
        }

        public override string Cookie
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public override string BytesReceived
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public override string BytesSent
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public override string ClientIP
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public override string ServerIP
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }


        int IComparable.CompareTo(object obj)
        {
            HTTPLog tempLog = obj is HTTPLog ? (HTTPLog)obj : null;
            if (tempLog == null)
            {
                throw new InvalidCastException();
            }

            //if less than zero then this instance is earlier than value, if zero its the same, and if greater then later
            return this._date.CompareTo(tempLog._date);
        }

    }
}
