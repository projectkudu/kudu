using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

///The purpose of this class is to provide an efficient way to parse log information. We aim to have this parser be able to parse log formats such as NCSA Combined Log Format, NCSA Separate log format
///NCSA Common, IIS, and possibly custom log formats. But for now we will focus on the W3C standard for log formats since its the default for all web servers.
namespace Kudu.Core.AnalyticsParser
{
    public class LogParser
    {
        private static string _fileName = null;
        private List<string> _fields = new List<string>();
        private HTTPLog _log;
        private LogFields[] logFormatFields;

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
            List<HTTPLog> logs = null;
            //Set fields for specific log format
            SetFields();
            //because we will be using this array for comparison against the fields of a log file, sort this array to use binary search and therefore have a O(logn) search
            //with O(n) iteration of the fields recorded in the log file
            Array.Sort(logFormatFields);
            try
            {
                logs = ParseW3CFormat();
            }
            catch (GrammarException e)
            {
                throw new GrammarException(e.Message);
            }

            return logs;
        }

        private List<HTTPLog> ParseW3CFormat()
        {
            List<HTTPLog> listLogs = new List<HTTPLog>();
            bool concateDateTime = false;
            //int dateColumn = 0;
            //int timeColumn = 0;

            //set up the fields for comparision purposes
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
                        //strip fields
                        StripFields(line,ref concateDateTime);
                    }

                    //if the line is not a directive line then its one of the HTTP log data
                    bool isDirectiveLine = line.StartsWith("#", StringComparison.OrdinalIgnoreCase);
                    if (!isDirectiveLine)
                    {
                        _log = new HTTPLog();
                        //get the data and follow the Grammar
                        string[] data = line.Split(default(Char[]), StringSplitOptions.RemoveEmptyEntries);                       
                        //using a foreach loop and follow through the grammar

                        /*
                        for(int i = 0; i < _fields.Count(); i++)
                        {
                            bool isValid = CheckGrammer(data[i], _fields[i]);
                            if (isValid)
                            {
                                switch(_fields[i])
                                {
                                    case "date":
                                        break;
                                }
                            }
                        }*/

                        foreach (string field in _fields)
                        {
                            //check if the field is the Date field and parse that field for this specific log and be sure that it follows the grammar for W3C
                            if (field.Equals(W3C_ExtendedConstants.DATE, StringComparison.OrdinalIgnoreCase))
                            {
                                bool isDate = CheckGrammer(data[count], W3C_ExtendedConstants.DATE);
                                if (isDate)
                                {
                                    //set the date for the HTTP log
                                    _log.Date = DateTime.Parse(data[count]);
                                }
                                else
                                {
                                    //System.Diagnostics.Trace.WriteLine("Excpetion: " + data[count]);
                                    throw new GrammarException("Text did not pass the grammar for dates according to the W3C standard.");
                                }
                            }
                            else if (field.Equals(W3C_ExtendedConstants.TIME, StringComparison.OrdinalIgnoreCase))
                            {
                                bool isTime = CheckGrammer(data[count], W3C_ExtendedConstants.TIME);
                                if (isTime)
                                {
                                    _log.Time = data[count];
                                }
                                else
                                {
                                    throw new GrammarException("Text did not pass the grammar for the time according to the W3C standard");
                                }
                            }
                            else if (field.Equals(W3C_ExtendedConstants.TIME_TAKEN, StringComparison.OrdinalIgnoreCase))
                            {
                                bool isTimeTaken = CheckGrammer(data[count], W3C_ExtendedConstants.TIME_TAKEN);
                                if (isTimeTaken)
                                {
                                    Trace.WriteLine(data[count]);
                                    _log.TimeTaken = data[count];
                                }
                            }
                            count++;
                        }
                        listLogs.Add(_log);
                    }
                }
            }
            return listLogs;
        }

        /// <summary>
        /// Set the fields for the specific log format that we are dealing with, by default this uses the W3C Extended format. On version 2 or later versions, 
        /// other log formats will be implemented.
        /// </summary>
        private void SetFields()
        {
            logFormatFields = new W3C_ExtendedField[22]
            {
                new W3C_ExtendedField("date"),
                new W3C_ExtendedField("time"), 
                new W3C_ExtendedField("c-ip"), 
                new W3C_ExtendedField("cs-username"),
                new W3C_ExtendedField("s-sitename"),
                new W3C_ExtendedField("s-computername"),
                new W3C_ExtendedField("s-ip"),
                new W3C_ExtendedField("s-port"),
                new W3C_ExtendedField("cs-method"),
                new W3C_ExtendedField("cs-uri-stem"),
                new W3C_ExtendedField("cs-uri-query"),
                new W3C_ExtendedField("sc-status"),
                new W3C_ExtendedField("sc-win32-status"),
                new W3C_ExtendedField("sc-bytes"),
                new W3C_ExtendedField("cs-bytes"),
                new W3C_ExtendedField("time-taken"),
                new W3C_ExtendedField("cs-version"),
                new W3C_ExtendedField("cs-host"),
                new W3C_ExtendedField(@"cs(User-Agent)"),
                new W3C_ExtendedField(@"cs(Cookie)"),
                new W3C_ExtendedField(@"cs(Referer)"),
                new W3C_ExtendedField("sc-substatus")
            };
        }

        /// <summary>
        /// Given a string where `#Fields` is the beginning, strip out all the fields that W3C format uses for logs
        /// </summary>
        /// <param name="unformattedFields"></param>
        public void StripFields(string unformattedFields, ref bool concateDateTime)
        {
            bool existFieldDate = false;
            bool existFieldTime = false;
            //fileds are delimitted by whitespace characters
            string[] tempFields = unformattedFields.Split(default(Char[]), StringSplitOptions.RemoveEmptyEntries);
            _fields.Clear();
            foreach (string field in tempFields)
            {
                //binarysearch returns a number of the index otherwise a negative number if not found
                if (Array.BinarySearch(logFormatFields, new W3C_ExtendedField(field)) >= 0)
                {
                    _fields.Add(field);
                }
            }

            concateDateTime = existFieldDate & existFieldTime;
        }

        private bool CheckGrammer(string data, string operation)
        {
            string spattern = null;
            switch (operation)
            {
                //check to see if the data fits the date format requred by W3C
                    
                case W3C_ExtendedConstants.DATE: 
                    spattern = "^\\d{4}-\\d{2}-\\d{2}$";
                    break;
                case W3C_ExtendedConstants.TIME: 
                    spattern = "^\\d{2}:\\d{2}:*[\\d{2}].*[\\d{1}]$";
                    break;
                case W3C_ExtendedConstants.TIME_TAKEN:
                    spattern = "^\\d*[\\.\\d*]";
                    break;
                case W3C_ExtendedConstants.BYTES:
                    spattern = "^\\d*$";
                    break;
            }

            return System.Text.RegularExpressions.Regex.IsMatch(data, spattern);
        }

        private bool isUnique()
        {
            return false;
        }
    }
}
