using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Diagnostics;
using Kudu.Core.AnalyticsDataLayer.CookieParser;

namespace Kudu.Core.AnalyticsEngineLayer.Metrics
{
    public class SessionNumberMetric:IMetric
    {
        private Dictionary<string, int> _uniqueSessionIds = new Dictionary<string, int>();

        public SessionNumberMetric(string metric)
        {
            //Metric name could be conversion, # of sessions, session lengths, bounce rate, leave rate, # of session ids
            MetricName = metric;
        }

        public string MetricName { get; set; }


        //the requirements are data fields that are needed from the log files 
        //public List<AnalyticsDataLayer.LogFields> MetricRequirements { get; set; }

        /// <summary>
        /// At the api controller level, time is already considered, now with the filtered logs dependent on the time the user specefied, perform the metric jobs
        /// for these log entries
        /// </summary>
        /// <param name="resource"></param>
        public void PerformMetricJob(AnalyticsDataLayer.HttpLog resource)
        {
            CookieCollection cookies = resource.Cookies;
            try
            {
                string sessionID = cookies[CookieConstants.D4DAD].Value;
                _uniqueSessionIds.Add(sessionID, 1);
            }
            catch (ArgumentException)
            {

            }
            catch (NullReferenceException e)
            {
                Trace.WriteLine(e.StackTrace);
            }
        }


        public object GetResult()
        {
            return _uniqueSessionIds.Count;
        }
    }
}
