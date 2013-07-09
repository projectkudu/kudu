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
        public AverageLatencyMetric()
        {
        }
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


        public void SetParameters(string args)
        {
            throw new NotImplementedException();
        }


        public void SetParameters(Dictionary<string, string> args)
        {
            throw new NotImplementedException();
        }


        public string GetMetricDescription
        {
            get { return "Determine the average from logs timetaken field."; }
        }
    }
}
