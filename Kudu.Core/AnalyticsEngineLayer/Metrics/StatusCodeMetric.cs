using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kudu.Core.AnalyticsEngineLayer.Metrics
{
    public class StatusCodeMetric:IMetric
    {
        //Key to search for within the dictionary object
        private const string STATUS_CODE_KEY = "statuscode";
        private int desiredStatusCode;
        public StatusCodeMetric()
        {
        }

        public StatusCodeMetric(string metricName)
        {
            MetricName = metricName;
        }
        private int _numStatus = 0;
        public string MetricName { get { return "Status Codes"; } set { } }

        public void PerformMetricJob(AnalyticsDataLayer.HttpLog resource)
        {
            //check the status code of the log
            if (resource.StatusCode == desiredStatusCode)
            {
                _numStatus++;
            }
        }

        public object GetResult()
        {
            return _numStatus;
        }

        /// <summary>
        /// Look for the keys needed in this metric and obtain the value, if not exist throw an exception
        /// </summary>
        /// <param name="args"></param>
        public void SetParameters(Dictionary<string, string> args)
        {
            string value;
            if (!args.TryGetValue(STATUS_CODE_KEY, out value))
            {
                throw new KeyNotFoundException("Could not find: " + STATUS_CODE_KEY);
            }
            else
            {
                desiredStatusCode = Convert.ToInt32(value);
            }
        }


        public string GetMetricDescription
        {
            get { return "Count how many occurences of a given status code"; }
        }
    }
}
