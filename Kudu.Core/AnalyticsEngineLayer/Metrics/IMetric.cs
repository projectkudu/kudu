using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Kudu.Core.AnalyticsDataLayer;

namespace Kudu.Core.AnalyticsEngineLayer.Metrics
{
    /// <summary>
    /// IMetric interface provides the underlying structure that programmers and designers need to define in their custom metric
    /// </summary>
    public interface IMetric
    {
        string MetricName { get; set; }
        //Depending on the metric and how a class derives this method, perform the computations to get the metric information
        void PerformMetricJob(HttpLog resource);
        object GetResult();
    }
}
