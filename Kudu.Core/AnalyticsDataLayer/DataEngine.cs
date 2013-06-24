using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Kudu.Core.LogHelper;
using System.IO;
using System.Diagnostics;

namespace Kudu.Core.AnalyticsDataLayer
{
    public class DataEngine:IDataLayerAPI
    {
        private Dictionary<string, long> _logFiles;

        public void SetLogDirectory(string path)
        {
            _logFiles = LogServiceHelper.GetDirectoryFiles(path);
        }

        /// <summary>
        /// Given a start and end date, return logs in a W3C_Extended object in that time interval
        /// Only return logs that have cookies within them
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <returns>Lines of DeSerialized logs in a given time interval</returns>
        public IEnumerable<W3C_Extended_Log> GetLines(DateTime start, DateTime end)
        {
            LogParser logParser = new LogParser();
            logParser.LogFormat = W3C_ExtendedConstants.FORMAT;
            logParser.setTimes(start, end);
            //iterate through our directory of files
            //Trace.WriteLine(start.ToString());
            foreach (string logFile in _logFiles.Keys)
            {
                //see that its capable to read this file
                logParser.FileName = logFile;
                if (!logParser.IsCapable)
                {
                    continue;
                }

                foreach (W3C_Extended_Log log in logParser.ParseW3CFormat())
                {
                    //Trace.WriteLine(log.dateTime.ToString());
                    //We are only interesting in logs that have cookies

                    /*
                    if (log.Cookies.Count == 0)
                    {
                        continue;
                    }
                    */
                    
                    if (log.UTCLogDateTime.CompareTo(start) >= 0 && log.UTCLogDateTime.CompareTo(end) < 0)
                    {
                        yield return log;
                    }
                }
            }
        }

        /// <summary>
        /// Given a start and end date, return logs in a W3C_Extended object in that time interval
        /// </summary>
        /// <returns>Lines of DeSerialized logs since the beginning of when logs were recorded to the current time</returns>
        public IEnumerable<W3C_Extended_Log> GetLines()
        {
            LogParser logParser = new LogParser();
            logParser.LogFormat = W3C_ExtendedConstants.FORMAT;
            //iterate through our directory of files
            foreach (string logFile in _logFiles.Keys)
            {
                //see that its capable to read this file
                logParser.FileName = logFile;
                if (!logParser.IsCapable)
                {
                    continue;
                }

                foreach (W3C_Extended_Log log in logParser.ParseW3CFormat())
                {
                    yield return log;
                }
            }
        }
    }
}
