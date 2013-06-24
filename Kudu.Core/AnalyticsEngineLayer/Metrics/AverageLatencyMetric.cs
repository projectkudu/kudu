using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kudu.Core.AnalyticsEngineLayer.Metrics
{
    public class AverageLatencyMetric:IMetric
    {
        private double total = 0;
        private int count = 0;
        public string MetricName { get { return "Average"; } set { } }

        public void PerformMetricJob(AnalyticsDataLayer.HttpLog resource)
        {
            total += resource.TimeTaken;
            count++;
        }

        public object GetResult()
        {
            if (count == 0)
            {
                return 0;
            }
            return total / count;
        }
    }
}
