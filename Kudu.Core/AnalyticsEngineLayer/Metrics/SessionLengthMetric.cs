using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Kudu.Core.AnalyticsDataLayer.Cookies;

namespace Kudu.Core.AnalyticsEngineLayer.Metrics
{
    /// <summary>
    /// This metric calculates the session length of each individual sessions for users
    /// </summary>
    public class SessionLengthMetric:IMetric
    {
        private Dictionary<string, int> _uniqueSessionIDs = new Dictionary<string, int>();
        private Dictionary<string, Interval> _sessionLengths = new Dictionary<string, Interval>();

        public string MetricName { get { return "Session Length"; } set { } }

        //In this method we will peform computations to find session lengths
        public void PerformMetricJob(AnalyticsDataLayer.HttpLog resource)
        {
            Dictionary<string, string> cookies = resource.Cookies;
            if (cookies != null)
            {
                string sessionID = "";        
                if (cookies.TryGetValue(CookieConstants.AZURE_D4DAD, out sessionID))
                {
                    GeneralizedCompute(sessionID, resource.UTCLogDateTime);
                }
                else if (cookies.TryGetValue(CookieConstants.D4DAD, out sessionID))
                {
                    GeneralizedCompute(sessionID, resource.UTCLogDateTime);
                }
            }
        }

        class Interval
        {
            public DateTime startTime;
            public DateTime endTime;
        }

        private bool GeneralizedAdd(string arg)
        {
            if (!_uniqueSessionIDs.ContainsKey(arg))
            {
                _uniqueSessionIDs.Add(arg, 1);
                return false;
            }
            return true;
        }

        private void GeneralizedCompute(string arg, DateTime time)
        {
            Interval interval = new Interval();
            //if the key does not exist then it was added to the dictionary keep track of this session ids startime
            if (!GeneralizedAdd(arg))
            {
                //if the session Id was unique then track the start time
                interval.startTime = time;
                _sessionLengths.Add(arg, interval);
            }
            else //if its already in the unique dictionary, then this must be the second or more'th time to read this session id
            {
                _sessionLengths[arg].endTime = time;
            }
        }

        public object GetResult()
        {
            Dictionary<string, TimeSpan> _computedSessionLengths = new Dictionary<string, TimeSpan>();
            TimeSpan difference;
            foreach(KeyValuePair<string, Interval> pair in _sessionLengths)
            {
                System.Diagnostics.Trace.WriteLine(pair.Value.startTime + " " + pair.Value.endTime);
                difference = pair.Value.endTime - pair.Value.startTime;
                _computedSessionLengths.Add(pair.Key, difference);
            }

            return _computedSessionLengths;
        }
    }
}
