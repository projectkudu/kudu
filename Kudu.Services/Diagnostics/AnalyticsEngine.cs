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
        private List<IMetric> _metricCollection = null;
        private List<Func<IMetric>> _factorMethods; 
        DataEngine dataEngine;

        public AnalyticsEngine()
        {
            _metricCollection = new List<IMetric>();
            _factorMethods = new List<Func<IMetric>>();
            dataEngine = new DataEngine();
            //dataEngine.SetLogDirectory(@"C:\Users\t-hawkf\Desktop\Logs\W3SVC1");
            dataEngine.SetLogDirectory(@"C:\Users\t-hawkf\Desktop\Azure Logs");
        }

        public string LogDirectory { get; set; }

        public string LogFormat { get; set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="functor"></param>
        public void AddMetricFactor(Func<IMetric> functor)
        {
            _factorMethods.Add(functor);
        }

        public Dictionary<string, object> RunEngine()
        {
            //iterate through the list of functions to create new instances of all the metrics we need
            foreach (Func<IMetric> func in _factorMethods)
            {
                _metricCollection.Add(func());
            }
            string result = string.Empty;
            //before running the engine make sure the LogFormat is specified and that there are metrics in the metricCollection
            if (_metricCollection == null || _metricCollection.Count == 0)
            {
                throw new Exception("Metric Collection is empty");
            }

            foreach (W3C_Extended_Log log in dataEngine.GetLines())
            {
                //for each metric in the metric collection, do the computation
                foreach (IMetric job in _metricCollection)
                {
                    job.PerformMetricJob(log);
                }
            }
            Dictionary<string, object> metricResults = new Dictionary<string, object>();
            foreach (IMetric job in _metricCollection)
            {
                metricResults.Add(job.MetricName, job.GetResult());
            }
            return metricResults;
        }

        public Dictionary<string, List<KeyValuePair<string, object>>> RunEngine(DateTime startTime, DateTime endTime, TimeSpan timeInterval)
        {
            MakeNewMetricCollection();
            //set up the dictioanry object that will take all the results that we need
            Dictionary<string, List<KeyValuePair<string, object>>> metricResults = new Dictionary<string, List<KeyValuePair<string, object>>>();
            //iterate for each of that unit of time 
            while (startTime < endTime)
            {
                //if we are going by the hour then add 1 Hour to starttime, by the day then add 1 day to startime, by weekly then add 7 days to starttime, by monthly
                //and by yearly....
                DateTime intermediateTime = startTime + timeInterval;
                //perform metric computation on all data from [startTime, intermediateTime)
                //afterwards clear the data that are in the metrics and compute metrics for the next set of data
                HelperFunction(startTime, intermediateTime);

                //after HelperFunction is called, the metrics for the data of timestamps [startTime, intermediateTime] should be completed, now organize the data
                foreach (IMetric job in _metricCollection)
                {
                    try
                    {
                        //make a new object of list for that key
                        metricResults.Add(job.MetricName, new List<KeyValuePair<string, object>>());
                    }
                    catch (ArgumentException)
                    {
                        //KEY already exist, just add the KeyValuePair
                    }
                    metricResults[job.MetricName].Add(new KeyValuePair<string, object>(startTime.ToString(), job.GetResult()));
                }

                startTime = intermediateTime;
                MakeNewMetricCollection();
            }
            return metricResults;
        }

        //public Dictionary<string, List<KeyValuePair<string, object>>> RunAlternativeEngine(DateTime start, DateTime end, TimeSpan timeInterval)
        public Dictionary<string, List<KeyValuePair<string, object>>> RunAlternativeEngine(DateTime start, DateTime end, TimeSpan timeInterval)
        {
            Dictionary<string, List<KeyValuePair<string, object>>> metricResults = new Dictionary<string, List<KeyValuePair<string, object>>>();
            List<W3C_Extended_Log> listLogs = new List<W3C_Extended_Log>();
            MakeNewMetricCollection();
            //Thought we are enumerating the data from the parser, the code is less complex if we have it all into a list and then perform our computations. Simple and quicker than the RunEngine method
            foreach (W3C_Extended_Log log in dataEngine.GetLines(start, end))
            {
                listLogs.Add(log);
            }

            //now that we have all of our data in memory go ahead and perform computations on our data with our metrics
            while (start < end)
            {
                //if we are going by the hour then add 1 Hour to starttime, by the day then add 1 day to startime, by weekly then add 7 days to starttime, by monthly
                //and by yearly....
                DateTime intermediateTime = start + timeInterval;
                //perform metric computation on all data from [startTime, intermediateTime)
                //afterwards clear the data that are in the metrics and compute metrics for the next set of data
                HelperFunction(start, intermediateTime, listLogs);

                //after HelperFunction is called, the metrics for the data of timestamps [startTime, intermediateTime] should be completed, now organize the data
                foreach (IMetric job in _metricCollection)
                {
                    try
                    {
                        //make a new object of list for that key
                        metricResults.Add(job.MetricName, new List<KeyValuePair<string, object>>());
                    }
                    catch (ArgumentException)
                    {
                        //KEY already exist, just add the KeyValuePair
                    }
                    metricResults[job.MetricName].Add(new KeyValuePair<string, object>(start.ToString(), job.GetResult()));
                }

                start = intermediateTime;
                MakeNewMetricCollection();
            }
            return metricResults;
        }
        /*
        public Dictionary<string, List<KeyValuePair<string, object>>> RunAlternativeEngine(DateTime start, DateTime end, TimeSpan timeInterval)
        {
            Dictionary<string, List<KeyValuePair<string, object>>> metricResults = new Dictionary<string, List<KeyValuePair<string, object>>>();
            List<W3C_Extended_Log> listLogs = new List<W3C_Extended_Log>();
            //since we have a timeinterval to follow, create to instances of DateTime that will increase by interval
            DateTime startTime = start;
            DateTime intermediateTime = startTime + timeInterval;
            MakeNewMetricCollection();
            //with a foreach loop, iterate through each logs as they are yielded and performJob
            foreach (W3C_Extended_Log log in dataEngine.GetLines(start, end))
            {
                Trace.WriteLine(log.LogDateTime.ToString());
                if (log.LogDateTime >= startTime && log.LogDateTime < intermediateTime)
                {
                    Trace.WriteLine("within bounds");
                    //perform jobs
                    foreach (IMetric job in _metricCollection)
                    {
                        job.PerformMetricJob(log);
                    }
                }
                else
                {
                    if (log.LogDateTime >= intermediateTime)
                    {
                        //flush the old data that we computed into the dictionary
                        foreach (IMetric job in _metricCollection)
                        {
                            try
                            {
                                //make a new object of list for that key
                                metricResults.Add(job.MetricName, new List<KeyValuePair<string, object>>());
                            }
                            catch (ArgumentException)
                            {
                                //KEY already exist, just add the KeyValuePair
                            }
                            metricResults[job.MetricName].Add(new KeyValuePair<string, object>(startTime.ToString(), job.GetResult()));
                        }

                        startTime = intermediateTime;
                        intermediateTime = startTime + timeInterval;
                    }
                    if (intermediateTime <= end)
                    {
                        MakeNewMetricCollection();
                        //since intermediate time is still less than or equal to end, we dont want to skip this log and iterate to the next log. We need to compute metrics on this one also
                        foreach (IMetric job in _metricCollection)
                        {
                            job.PerformMetricJob(log);
                        }
                    }
                }
            }

            return metricResults;
        }*/


        private void HelperFunction(DateTime start, DateTime end)
        {
            //get all the data that we need from one datetime instance to another then from there, work on the data to get it for specefic intervals
            foreach (W3C_Extended_Log log in dataEngine.GetLines(start, end))
            {
                //for each metric in the metric collection, do the computation
                foreach (IMetric job in _metricCollection)
                {
                    job.PerformMetricJob(log);
                }
            }
        }

        private void HelperFunction(DateTime start, DateTime end, List<W3C_Extended_Log> listLogs)
        {
            foreach (W3C_Extended_Log log in listLogs)
            {
                if (log.UTCLogDateTime >= start && log.UTCLogDateTime < end)
                {
                    foreach (IMetric job in _metricCollection)
                    {
                        job.PerformMetricJob(log);
                    }
                }
                else
                {
                    continue;
                }
            }
        }

        private void MakeNewMetricCollection()
        {
            _metricCollection.Clear();
            //iterate through the list of functions to create new instances of all the metrics we need
            foreach (Func<IMetric> func in _factorMethods)
            {
                _metricCollection.Add(func());
            }
        }
    }

    class Interval
    {
        DateTime start;
        DateTime end;
        string unitOftime;
        public Interval(DateTime start, DateTime end, string timeInterval)
        {
            this.start = start;
            this.end = end;
            this.unitOftime = timeInterval;
        }

        public override string ToString()
        {
            switch(unitOftime)
            {
                default: return string.Empty;
            }
        }
    }
}
