using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

///The purpose of this class is to provide an efficient way to parse log information. We aim to have this parser be able to parse log formats such as NCSA Combined Log Format, NCSA Separate log format
///NCSA Common, IIS, and possibly custom log formats. But for now we will focus on the W3C standard for log formats since its the default for all web servers.
namespace Kudu.Core.Analytics_DataLayer
{
    public class LogParser
    {
        private static string _fileName = null;
        private List<string> _fields = new List<string>();
        private HTTPLog _log;

        /// <summary>
        /// Get or Set the filename for the logParser to use
        /// </summary>
        public string FileName
        {
            get{ return _fileName;}
            set { _fileName = value; }
        }

        public List<HTTPLog> Parse()
        {
            if (_fileName == null)
            {
                throw new NullReferenceException();
            }
            List<HTTPLog> logs = ParseW3CFormat();
            return logs;
        }

        private List<HTTPLog> ParseW3CFormat()
        {
            List<HTTPLog> listLogs = new List<HTTPLog>();
            using (StreamReader reader = new StreamReader(_fileName))
            {
                string line;
                //strip the fields line by line
                int count = 0;
                while ((line = reader.ReadLine()) != null)
                {
                    count = 0;
                    //W3C Extended Log Format has a Field Directive explaining the fields that are tracked within the log file, parse the fields to know what methods
                    //should be called to parse the text
                    bool isFields = line.StartsWith(W3C_ExtendedConstants.FIELD_DIRECTIVE, StringComparison.OrdinalIgnoreCase);
                    if (isFields)
                    {
                        StripFields(line);
                    }

                    //if the line is not a directive line then its one of the HTTP log data
                    bool isDirectiveLine = line.StartsWith("#", StringComparison.OrdinalIgnoreCase);
                    if (!isDirectiveLine)
                    {
                        _log = new HTTPLog();
                        //get the data and follow the Grammar
                        string[] data = line.Split(default(Char[]), StringSplitOptions.RemoveEmptyEntries);                       
                        //using a foreach loop and follow through the grammar
                        foreach (string field in _fields)
                        {

                            if (field.Equals(W3C_ExtendedConstants.DATE, StringComparison.OrdinalIgnoreCase))
                            {
                                bool isDate = CheckGrammer(data[count], W3C_ExtendedConstants.DATE);
                                if (isDate)
                                {
                                    //set the date for the HTTP log
                                    _log.Date = DateTime.Parse(data[count]);
                                }

                                Console.WriteLine(_log.Date.ToShortDateString());
                                count++;
                            }
                        }
                        listLogs.Add(_log);
                    }
                }
            }
            return listLogs;
        }

        /// <summary>
        /// Given a string where `#Fields` is the beginning, strip out all the fields that W3C format uses for logs
        /// </summary>
        /// <param name="unformattedFields"></param>
        public void StripFields(string unformattedFields)
        {
            //fileds are delimitted by whitespace characters
            string[] tempFields = unformattedFields.Split(default(Char[]), StringSplitOptions.RemoveEmptyEntries);
            foreach (string field in tempFields)
            {
                _fields.Add(field);
            }
        }

        private bool CheckGrammer(string data, string operation)
        {
            string spattern;
            switch (operation)
            {
                //check to see if the data fits the date format requred by W3C
                    
                case W3C_ExtendedConstants.DATE: spattern = "^\\d{4}-\\d{2}-\\d{2}$";
                    bool answer = System.Text.RegularExpressions.Regex.IsMatch(data, spattern);
                    return answer;
            }
            return false;
        }


    }
}
