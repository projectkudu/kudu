using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using Kudu.Core.AnalyticsEngineLayer.Metrics;
using Kudu.Core.AnalyticsDataLayer;

namespace Kudu.Services.Diagnostics
{
    public class AnalyticsEngine
    {
        //have a list of all the metrics that we are interested in
        List<IMetric> metricCollection = null;
        DataEngine dataEngine;

        public AnalyticsEngine()
        {
            metricCollection = new List<IMetric>();
            dataEngine = new DataEngine();
            dataEngine.SetLogDirectory(@"C:\Users\t-hawkf\Desktop\Logs\W3SVC1");
        }

        public string LogDirectory { get; set; }

        public string LogFormat { get; set; }

        /// <summary>
        /// Given the metric, add it into the collection of metrics to be calculated
        /// </summary>
        /// <param name="metric"></param>
        public void AddMetric(IMetric metric)
        {
            metricCollection.Add(metric);
        }

        public void RunEngine()
        {
            //before running the engine make sure the LogFormat is specified and that there are metrics in the metricCollection
            if (metricCollection == null || metricCollection.Count == 0)
            {
                throw new Exception("Metric Collection is empty");
            }

            foreach (W3C_Extended_Log log in dataEngine.GetLines(new DateTime(2013, 6, 12, 17, 19, 0), new DateTime(2013, 6, 12, 21, 19, 0)))
            {
                //for each metric in the metric collection, do the computation
                foreach (IMetric job in metricCollection)
                {
                    Trace.WriteLine(job.PerformMetricJob(log));
                }
                //Trace.WriteLine(log);
            }
        }

        public void RunEngine(string startTime, string endTime)
        {

        }
    }
}
