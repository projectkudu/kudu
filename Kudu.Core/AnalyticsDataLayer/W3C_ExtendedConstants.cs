using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kudu.Core.AnalyticsDataLayer
{
    public static class W3C_ExtendedConstants
    {
        public const string FORMAT = "W3C_EXTENDED";
        public const string FIELD_DIRECTIVE = "#Fields";
        //Identifiers <identifier>
        public const string DATE = "date";
        public const string TIME = "time";
        public const string TIME_TAKEN = "time-taken";
        public const string BYTES = "bytes";
        public const string CACHED = "cached";

        //Identifiers that require a prefix <prefix-identifier>
        public const string CLIENT_IP = "c-ip";
        public const string USER_NAME = "cs-username";
        public const string SERVICE_NAME = "s-sitename";
        public const string SERVER_NAME = "s-computername";
        public const string SERVER_IP = "s-ip";
        public const string SERVER_PORT = "s-port";
        public const string DNS = "dns";
        public const string STATUS = "sc-status";
        public const string WIN32_STATUS = "sc-win32-status";
        public const string COMMENT = "comment";
        public const string METHOD = "cs-method";
        public const string URI = "uri";
        public const string URI_STEM = "cs-uri-stem";
        public const string URI_QUERY = "cs-uri-query";
        public const string BYTES_SENT = "sc-bytes";
        public const string BYTES_RECEIVED = "cs-bytes";
        public const string PROTOCOL_VERSION = "cs-version";
        public const string HOST = "cs-host";
        public const string USER_AGENT = @"cs(user-agent)";
        public const string COOKIE = @"cs(cookie)";
        public const string REFERRER = @"cs(referrer)";
        public const string PROTOCOL_SUBSTATUS = "sc-substatus";
    }
}
