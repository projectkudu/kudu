using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.IO;
using System.Net.Http;
using System.Web.Http;
using System.Web;
using System.Runtime.Remoting;
using System.Security.Permissions;
using System.Security;
using System.Diagnostics;
using Kudu.Core.AnalyticsDataLayer;
using Kudu.Core.LogHelper;
using Kudu.Core.AnalyticsEngineLayer.Metrics;
using Newtonsoft.Json;
namespace Kudu.Services.Diagnostics
{
    public class AnalyticsController : ApiController
    {
        //string path = @"C:\Users\t-hawkf\Desktop\TempLogs";
        string path = @"C:\Users\t-hawkf\Desktop\Logs\W3SVC1";
        //private Dictionary<string, long> _logFiles;
        public string testing = null;
        //private List<W3C_Extended_Log> logs;
        //Kudu.Core.IEnvironment environment;
        private AnalyticsEngine _analytics;

        public AnalyticsController()
        {
            //make an instance of the Anaylytics Engine
            _analytics = new AnalyticsEngine();
            _analytics.LogDirectory = path;
            //TestData();
            //_logFiles = LogServiceHelper.GetDirectoryFiles(path);
            //logs = ScanIISFiles();
            testing = "hello word";
        }

        [HttpGet]
        public string SessionCount()
        {
            return "Hello world";
        }

        [HttpGet]
        //Analytics API routing: /diagnostics/analytics?{metrics=metricValues}&{start=datetime}&{end=datetime}&{interval=timeInterval}&{arguments= %7B%22{key}%22%3A%22{value}%22%7D}
        //When customers are using this API they have to append a URL encoded parameters for their metric computations to work
        //public Dictionary<string, List<KeyValuePair<string, object>>> GetAnalytics(String metrics, DateTime start, DateTime end, TimeSpan interval, string arguments)
        //public Dictionary<string, string> GetAnalytics(String metrics, DateTime start, DateTime end, TimeSpan interval, Dictionary<string,string> arguments)
        // Returns a JSON wrapping of the data
        public string GetAnalytics(String metrics, DateTime start, DateTime end, TimeSpan interval, string arguments)
        {
            Trace.WriteLine(arguments);
            //convert the JSON data into dictionary
            Dictionary<string, string> parameters = JsonConvert.DeserializeObject<Dictionary<string, string>>(arguments);

            //based on the metrics given, add that metric to the Analytics Engine
            _analytics.AddMetricFactor(() => ActivateMetrics(metrics, parameters));
            /*
            if (metrics != null)
            {
                string[] requestedMetrics = metrics.Split(',');

                foreach (string requestedMetric in requestedMetrics)
                {
                    _analytics.AddMetricFactor(() => ActivateMetrics(requestedMetric,arguments));
            
                }
            }*/

            Dictionary<string, List<KeyValuePair<string, object>>> result = _analytics.RunAlternativeEngine(start, end, interval);
            string jsonString = JsonConvert.SerializeObject(result);
            return jsonString;
        }

        [HttpGet]
        public string GetAvailableMetrics()
        {
            var availableMetrics = _analytics.GetMetricsDescriptions();
            string jsonVersion = JsonConvert.SerializeObject(availableMetrics);
            return jsonVersion;
        }

        [NonAction]
        private IMetric ActivateMetrics(string metric, Dictionary<string,string> args)
        {
            ObjectHandle handle;
            try
            {
                handle = Activator.CreateInstance(typeof(IMetric).Assembly.FullName, "Kudu.Core.AnalyticsEngineLayer.Metrics." + metric);
            }
            catch (Exception e)
            {
                Trace.WriteLine(e);
                return null;
            }
            
            //return the wrapped object
            IMetric m = (IMetric)handle.Unwrap();
            try
            {
                m.SetParameters(args);
            }
            catch (KeyNotFoundException)
            {
                throw new Exception("Metric cannot be computed without arguments");
            }
            
            return m;
        }

        [HttpGet]
        public string GetValue(string metrics)
        {
            Trace.WriteLine(metrics);
            return null;
        }

        [NonAction]
        private void AddMetrics(string arg)
        {
            switch (arg)
            {
                case MetricNames.NUM_SESSIONS:
                    _analytics.AddMetricFactor(() => new SessionNumberMetric("# of sessions"));
                    break;
                case MetricNames.SESSION_LENGTH:
                    _analytics.AddMetricFactor(() => new SessionLengthMetric());
                    break;
                case MetricNames.STATUS_CODES:
                    _analytics.AddMetricFactor(() => new StatusCodeMetric("status code 200"));
                    break;
                case MetricNames.AVERAGE_LATENCY:
                    _analytics.AddMetricFactor(() => new AverageLatencyMetric());
                    break;
                default: throw new HttpException(404, "Invalid metric name");
            }
        }

        [NonAction]
        private void AllMetrics()
        {
            _analytics.AddMetricFactor(() => new SessionNumberMetric("# of sessions"));
            _analytics.AddMetricFactor(() => new SessionLengthMetric());
            _analytics.AddMetricFactor(() => new StatusCodeMetric("status code 200"));
            _analytics.AddMetricFactor(() => new AverageLatencyMetric());
        }
    }
}