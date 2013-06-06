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
using Kudu.Core.AnalyticsParser;

namespace Kudu.Services.Diagnostics
{
    public class AnalyticsController : ApiController
    {
        string path = @"C:\Users\t-hawkf\Desktop\TempLogs";
        private Dictionary<string, long> _logFiles;
        
        //Kudu.Core.IEnvironment environment;

        public AnalyticsController()
        {
            _logFiles = GetDirectoryFiles(path);
            ScanFiles();
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
        private void ScanFiles()
        {
            List<HTTPLog> httpLogs = new List<HTTPLog>();
            LogParser logParser = new LogParser();
            
            foreach (string logFile in _logFiles.Keys)
            {
                Trace.WriteLine(logFile);
                logParser.FileName = logFile;
                List<HTTPLog> temp = null;
                try
                {
                    temp = logParser.Parse();
                }
                catch (GrammarException)
                {
                    Trace.WriteLine("cool");
                }
                httpLogs.AddRange(temp);
                break;
            }

            foreach (HTTPLog log in httpLogs)
            {
                //Trace.WriteLine(log.Date.ToString());
            }
        }

        /// <summary>
        /// Given a path to a directory, scan all the .log, .txt files in the directory and subdirectories to store information in memory and work with the data
        /// </summary>
        /// <param name="directory">The path to the directory in which Kudu is storing log files. (*note same location in azure)</param>
        /// <returns>Dictionary where the key is the fullname or absolute path of the log file that we scanned and the value is the length of that file.</returns>
        [NonAction]
        private Dictionary<string, long> GetDirectoryFiles(string directory)
        {
            //using a stack, store the directory names in the data structure, and follow a post-order traversal in traversing the log files
            Stack<string> stack = new Stack<string>();
            Dictionary<string, long> files = new Dictionary<string, long>();
            //begin by pushing the directory where the files are
            stack.Push(directory);
            string currentDirectory = null;
            while (stack.Count > 0)
            {
                //FIFO get the top directory to scan through
                currentDirectory = stack.Pop();
                string[] subDirectories;
                subDirectories = Directory.GetDirectories(currentDirectory);
                //traverse each file and add the file path to the dictionary
                foreach (string fileName in Directory.GetFiles(currentDirectory))
                {
                    files.Add(fileName, new FileInfo(fileName).Length);
                    //Trace.WriteLine(fileName);
                }
                
                //after adding all the files to the dictionary for the current directory, push the paths of the subdirectories to the stack and continue the loop
                foreach (string subDirectory in subDirectories)
                {
                    stack.Push(subDirectory);
                }
            }
            return files;
        }
    }
}