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
        /// <returns></returns>
        public string PerformMetricJob(AnalyticsDataLayer.HttpLog resource)
        {
            Dictionary<string, int> uniqueSessionIds = new Dictionary<string, int>();

            //for W3C_Extended, Get the cookies and count the number of unique sessions ids and return a string representation of a int
            switch(LogFormat)
            {
                case AnalyticsDataLayer.LogFormat.W3C_EXTENDED:
                    //for W3C Extended grab the cookie collection
                    CookieCollection cookies = resource.Cookies;
                    try
                    {
                        string sessionID = cookies[CookieConstants.WA_WEBSITE_SID].Name;
                        uniqueSessionIds.Add(sessionID, 1);
                    }
                    catch (ArgumentException e)
                    {
                        Trace.WriteLine(e.StackTrace);
                    }
                    catch (NullReferenceException)
                    {
                        Trace.WriteLine("null reference");
                    }
                    break;
            }
            return Convert.ToString(uniqueSessionIds.Count);
        }

        /// <summary>
        /// Define the Log format that this metric will focus on
        /// </summary>
        public string LogFormat { get; set; }
    }
}
