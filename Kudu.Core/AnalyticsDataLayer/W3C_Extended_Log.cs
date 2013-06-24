using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kudu.Core.AnalyticsDataLayer
{

    public sealed class W3C_Extended_Log : HttpLog, IComparable
    {

        /// <summary>
        /// Initialize a new instance of the HTTPLog class
        /// </summary>

        //TODO make the below private or call it UTC date or time
        public override DateTime Date { get; set; }
        
        public override DateTime Time {get; set;}

        public override DateTime UTCLogDateTime { get; set; }

        public override int StatusCode {get; set;}

        public int Win32_Status{get; set;}

        public override string URIRequested{get;set;}

        public override string TypeRequest{get; set;}

        public override Dictionary<string,string> Cookies{get;set;}

        public override int BytesReceived{get;set;}

        public override int BytesSent{get; set;}

        public override System.Net.IPAddress ClientIP{get; set;}

        public override System.Net.IPAddress ServerIP{get; set;}

        public string UserName{get; set;}

        public override Uri Referrer{get; set;}

        public override int TimeTaken{get; set;}

        public string ProtocolVersion { get; set;}

        public string Host {get; set;}

        public string UserAgent {get; set;}

        public string ServerSiteName {get; set;}

        public int ServerPort {get; set;}

        public string ProtocolSubStatus {get; set;}

        public string UriStem {get; set;}

        public string UriQuery {get; set;}

        public override string ToString()
        {
            string summarize = null;
            string fullURL = Host + UriStem;

            if (!UriQuery.Equals("-"))
            {
                fullURL += UriQuery;
            }

            /*
            foreach (CookieParser.Cookie cookie in Cookies)
            {
                summarize += fullURL + " " + Date.ToShortDateString() + " " + Time.ToShortTimeString() + " " + cookie.Key + " " + cookie.Value + " " + BytesSent.ToString() + " " + TimeTaken;
                System.Diagnostics.Trace.WriteLine(summarize);
            }*/
            return summarize;
        }

        int IComparable.CompareTo(object obj)
        {
            //W3C_Extended_Log tempLog = obj is W3C_Extended_Log ? (W3C_Extended_Log)obj : null;
            W3C_Extended_Log tempLog = (W3C_Extended_Log)obj;
            if (tempLog == null)
            {
                throw new InvalidCastException();
            }

            //if less than zero then this instance is earlier than value, if zero its the same, and if greater then later
            return this.Date.CompareTo(tempLog.Date);
        }
    }
}
