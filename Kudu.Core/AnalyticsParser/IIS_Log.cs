using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kudu.Core.AnalyticsParser
{

    public class IIS_Log : HttpLog, IComparable
    {
        //TODO: remove the private variables and make the getters/setters look like Date

        /// <summary>
        /// Initialize a new instance of the HTTPLog class
        /// </summary>
        public IIS_Log()
        {
        }

        public override DateTime Date { get; set; }
       
        
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

        public override int StatusCode
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

        public int Win32_Status
        {
            get
            {
                return _win32_Status;
            }
            set
            {
                _win32_Status = value;
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

        public override System.Net.CookieCollection Cookies
        {
            get
            {
                return _cookies;
            }
            set
            {
                _cookies = value; ;
            }
        }

        public override long BytesReceived
        {
            get
            {
                return _bytesReceived;
            }
            set
            {
                _bytesReceived = value;
            }
        }

        public override long BytesSent
        {
            get
            {
                return _bytesSent;
            }
            set
            {
                _bytesSent = value;
            }
        }

        public override System.Net.IPAddress ClientIP
        {
            get
            {
                return _clientIP_Address;
            }
            set
            {
                _clientIP_Address = value;
            }
        }

        public override System.Net.IPAddress ServerIP
        {
            get
            {
                return _serverIP_Address;
            }
            set
            {
                _serverIP_Address = value;
            }
        }

        public string UserName
        {
            get
            {
                return _username;
            }
            set
            {
                _username = value;
            }
        }


        int IComparable.CompareTo(object obj)
        {
            IIS_Log tempLog = obj is IIS_Log ? (IIS_Log)obj : null;
            if (tempLog == null)
            {
                throw new InvalidCastException();
            }

            //if less than zero then this instance is earlier than value, if zero its the same, and if greater then later
            return this._date.CompareTo(tempLog._date);
        }


        public override Uri Referrer
        {
            get
            {
                return _refferrer;
            }
            set
            {
                _refferrer = value;
            }
        }

        public override float TimeTaken
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

        public string ProtocolVersion
        {
            get
            {
                return _protocolVersion;
            }
            set
            {
                _protocolVersion = value;
            }
        }

        public string Host
        {
            get
            {
                return _host;
            }
            set
            {
                _host = value;
            }
        }

        public string UserAgent
        {
            get
            {
                return _userAgent;
            }
            set
            {
                _userAgent = value;
            }
        }

        public string ServerSiteName
        {
            get
            {
                return _serverSitename;
            }
            set
            {
                _serverSitename = value;
            }
        }

        public int ServerPort
        {
            get
            {
                return _serverPort;
            }
            set
            {
                _serverPort = value;
            }
        }

        public string ProtocolSubStatus
        {
            get
            {
                return _protocolSubStatus;
            }
            set
            {
                _protocolSubStatus = value;
            }
        }
        public string UriStem
        {
            get
            {
                return _uriStem;
            }
            set
            {
                _uriStem = value;
            }
        }
        public string UriQuery
        {
            get
            {
                return _uriQuery;
            }
            set
            {
                _uriQuery = value;
            }
        }

        public override string ToString()
        {
            string summarize = null;
            string fullURL = Host + UriStem;
            if (!UriQuery.Equals("-"))
            {
                fullURL += UriQuery;
            }
            foreach (System.Net.Cookie cookie in _cookies)
            {
                summarize += fullURL + " " +_date.ToShortDateString() + " " + cookie.Name + " " + cookie.Value;
            }
            return summarize;
        }
    }
}
