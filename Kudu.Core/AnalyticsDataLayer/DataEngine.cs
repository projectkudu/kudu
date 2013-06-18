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
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <returns>Lines of DeSerialized logs in a given time interval</returns>
        public IEnumerable<W3C_Extended_Log> GetLines(DateTime start, DateTime end)
        {
            LogParser logParser = new LogParser();
            logParser.LogFormat = W3C_ExtendedConstants.FORMAT;
            //iterate through our directory of files
            Trace.WriteLine(start.ToString());
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
                    if (log.dateTime.CompareTo(start) >= 0 && log.dateTime.CompareTo(end) < 0)
                    {
                        yield return log;
                    }
                }
            }
        }
    }
}
