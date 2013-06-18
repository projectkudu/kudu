using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.IO;
using System.Net.Http;
using System.Web.Http;
using System.Security.Permissions;
using System.Security;
using System.Diagnostics;
using Kudu.Core.AnalyticsDataLayer;
using Kudu.Core.LogHelper;
using Kudu.Core.AnalyticsEngineLayer.Metrics;

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

        /// <summary>
        /// Gets the number of sessions for this site since the beginning of the logged data
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public string GetSessionCount()
        {
            //add a metric that covers unique sessions to the AnalyticsEngine
            SessionNumberMetric metric = new SessionNumberMetric("# of sessions");
            //Make sure the log format is W3C EXtended
            metric.LogFormat = LogFormat.W3C_EXTENDED;
            _analytics.AddMetric(metric);
            _analytics.RunEngine();
            return String.Empty;
        }

        [HttpGet]
        public string GetNumberOfSessions(string startTime, string endTime)
        {
            throw new NotImplementedException();
        }

        [HttpGet]
        public string TestData()
        {
            DataEngine dataEngine = new DataEngine();
            dataEngine.SetLogDirectory(path);
            String data = String.Empty;
            foreach (W3C_Extended_Log log in dataEngine.GetLines(new DateTime(2013, 6, 12, 17, 19, 0), new DateTime(2013, 6, 12, 21, 19, 0)))
            {
                Trace.WriteLine(log);
            }
            return data;
        }

        public string GetName()
        {
            return "Hawk";
        }
        // GET api/<controller>
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

        // GET api/<controller>/5
        public string Get(int id)
        {
            return "value";
        }

        // POST api/<controller>
        public void Post([FromBody]string value)
        {
        }

        // PUT api/<controller>/5
        public void Put(int id, [FromBody]string value)
        {
        }

        // DELETE api/<controller>/5
        public void Delete(int id)
        {
        }

        /// <summary>
        /// Given that the dictionary of the files are there, start scanning each file and get the information that we need to store them in memory
        /// </summary>
        [NonAction]

        
        private List<W3C_Extended_Log> ScanIISFiles()
        {
            List<W3C_Extended_Log> httpLogs = new List<W3C_Extended_Log>();
            LogParser logParser = new LogParser();
            /*
            foreach (string logFile in _logFiles.Keys)
            {
                logParser.FileName = logFile;
                //check if the parser is capable of parsing that file and if so then parse and if not then go on to next file
                if (!logParser.IsCapable)
                {
                    continue;
                }
                //List<W3C_Extended_Log> temp = logParser.Parse();
                foreach (W3C_Extended_Log log in logParser.ParseW3CFormat())
                {
                    Trace.WriteLine(log);
                }
                //httpLogs.AddRange(temp);
            }*/

            return httpLogs;
        }
    }
}