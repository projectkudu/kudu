using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kudu.Core.AnalyticsParser
{
    public abstract class Log
    {
        /// <summary>
        /// All logs have some type of date information, extend this class to have your own format of how the date may be formatted
        /// Eg, YYYY-MM-DD, MM-DD-YY, MM/DD/YYYY, MM/DD/YY
        /// </summary>
        public abstract DateTime Date{get;set;}

        /// <summary>
        /// Time at which the activity occured
        /// </summary>
        public abstract string Time{get;set;}

        public abstract string BytesReceived{ get;set;}

        public abstract string BytesSent{ get; set;}

        public abstract string StatusCode{get;set;}

        public abstract string URIRequested{get;set;}

        public abstract string Cookie{get;set;}

        public abstract string TimeTaken{get;set;}

        public abstract string TypeRequest { get; set; }

        public abstract string ClientIP {get; set;}

        public abstract string ServerIP { get; set; }


    }
}
