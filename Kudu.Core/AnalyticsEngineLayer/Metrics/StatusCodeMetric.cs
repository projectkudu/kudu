using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kudu.Core.AnalyticsEngineLayer.Metrics
{
    public class StatusCodeMetric:IMetric
    {
        public StatusCodeMetric(string metricName)
        {
            MetricName = metricName;
        }
        private int _num200s = 0;
        public string MetricName { get; set; }

        public void PerformMetricJob(AnalyticsDataLayer.HttpLog resource)
        {
            //check the status code of the log
            if (resource.StatusCode == 200)
            {
                _num200s++;
            }
        }

        public object GetResult()
        {
            return _num200s;
        }
    }
}
