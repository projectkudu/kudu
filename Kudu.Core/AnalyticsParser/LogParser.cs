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
    //TODO: can you handle this parsing of file, 
    public class LogParser
    {
        private static string _fileName = null;
        private List<string> _fields = new List<string>();
        private IIS_Log _log;
        private LogFields[] logFormatFields;

        /// <summary>
        /// Get or Set the filename for the logParser to use
        /// </summary>
        public string FileName
        {
            get{ return _fileName;}
            set { _fileName = value; }
        }

        public List<IIS_Log> Parse()
        {
            if (_fileName == null)
            {
                throw new NullReferenceException();
            }
            List<IIS_Log> logs = null;
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

        // return IEnumerable<T> 
        private List<IIS_Log> ParseW3CFormat()
        {
            List<IIS_Log> listLogs = new List<IIS_Log>();
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
                        _log = new IIS_Log();
                        //get the data and follow the Grammar
                        string[] data = line.Split(default(Char[]), StringSplitOptions.RemoveEmptyEntries);                       
                        //using a foreach loop and follow through the grammar
                        for(int i = 0; i < _fields.Count(); i++)
                        {
                            //check that the text fits the grammar
                            bool isValid = CheckGrammer(data[i], _fields[i].ToLower());
                            
                            if (isValid)
                            {
                                
                                switch(_fields[i].ToLower())
                                {
                                    case W3C_ExtendedConstants.DATE:
                                        //Trace.WriteLine(data[i]);
                                        _log.Date = DateTime.Parse(data[count]);
                                        break;
                                    case W3C_ExtendedConstants.TIME:
                                        //Trace.WriteLine(data[i]);
                                        _log.Time = data[i];
                                        break;
                                    case W3C_ExtendedConstants.TIME_TAKEN:
                                        //Trace.WriteLine(data[i]);
                                        //_log.TimeTaken = data[i];
                                        break;
                                    case W3C_ExtendedConstants.BYTES:
                                        _log.BytesSent = Convert.ToInt64(data[i]);
                                        break;
                                    case W3C_ExtendedConstants.CACHED:
                                        //nothing
                                        break;
                                    case W3C_ExtendedConstants.CLIENT_IP:
                                        try
                                        {
                                            
                                            _log.ClientIP = System.Net.IPAddress.Parse(data[i]);
                                        }
                                        catch (FormatException)
                                        {
                                            _log.ClientIP = System.Net.IPAddress.Parse("0");
                                        }
                                        break;
                                    case W3C_ExtendedConstants.USER_NAME:
                                        _log.UserName = data[i];
                                        break;
                                    case W3C_ExtendedConstants.SERVICE_NAME:
                                        _log.ServerSiteName = data[i];
                                        break;
                                    case W3C_ExtendedConstants.SERVER_IP:
                                        try
                                        {
                                            _log.ServerIP = System.Net.IPAddress.Parse(data[i]);
                                        }
                                        catch (FormatException)
                                        {
                                            _log.ServerIP = System.Net.IPAddress.Parse("0");
                                        }
                                        break;
                                    case W3C_ExtendedConstants.SERVER_PORT:
                                        _log.ServerPort = Convert.ToInt16(data[i]);
                                        break;
                                    case W3C_ExtendedConstants.DNS:
                                        //nothing
                                        break;
                                    case W3C_ExtendedConstants.STATUS:
                                        _log.StatusCode = Convert.ToInt16(data[i]);
                                        break;
                                    case W3C_ExtendedConstants.WIN32_STATUS:
                                        _log.Win32_Status = Convert.ToInt16(data[i]);
                                        break;
                                    case W3C_ExtendedConstants.COMMENT:
                                        //nothing
                                        break;
                                    case W3C_ExtendedConstants.METHOD:
                                        _log.TypeRequest = data[i];
                                        break;
                                    case W3C_ExtendedConstants.URI:
                                        //nothing
                                        break;
                                    case W3C_ExtendedConstants.URI_STEM:
                                        _log.UriStem = data[i];
                                        break;
                                    case W3C_ExtendedConstants.URI_QUERY:
                                        _log.UriQuery = data[i];
                                        break;
                                    case W3C_ExtendedConstants.BYTES_SENT:
                                        _log.BytesSent = Convert.ToInt64(data[i]);
                                        break;
                                    case W3C_ExtendedConstants.BYTES_RECEIVED:
                                        _log.BytesSent = Convert.ToInt64(data[i]);
                                        break;
                                    case W3C_ExtendedConstants.PROTOCOL_VERSION:
                                        _log.ProtocolVersion = data[i];
                                        break;
                                    case W3C_ExtendedConstants.HOST:
                                        _log.Host = data[i];
                                        break;
                                    case W3C_ExtendedConstants.USER_AGENT:
                                        _log.UserAgent = data[i];
                                        break;
                                    case W3C_ExtendedConstants.COOKIE:
                                        CookieParser.CookieParser cookieParser = new CookieParser.CookieParser();
                                        _log.Cookies = cookieParser.ExtractServerHeaderResponseCookies(data[i]);
                                        break;
                                    case W3C_ExtendedConstants.REFERRER:
                                        _log.Referrer = new Uri(data[i]);
                                        break;
                                }
                            }//end of if statement
                        }

                        //TODO: look into Enumeration, yield return _log;
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

        //TODO: if something is not according to the format keep track of the errors and present it to user
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
                default: //if none of the aforementioned cases apply then that means we are using fields that dont necessarily need grammar checks for instance usernames, 
                    return true;
            }

            return System.Text.RegularExpressions.Regex.IsMatch(data, spattern);
        }

        private bool isUnique()
        {
            return false;
        }
    }
}
