using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kudu.Core.AnalyticsParser
{
    class W3C_ExtendedFormat
    {

    }

    public static class W3C_ExtendedConstants
    {
        public const string FIELD_DIRECTIVE = "#Fields";


        //Identifiers <identifier>
        public const string DATE = "date";
        public const string TIME = "time";
        public const string TIME_TAKEN = "time-taken";
        public const string BYTES = "bytes";
        public const string CACHED = "cached";

        //Identifiers that require a prefix <prefix-identifier>
        public const string IP = "ip";
        public const string DNS = "dns";
        public const string STATUS = "status";
        public const string COMMENT = "comment";
        public const string METHOD = "method";
        public const string URI = "uri";
        public const string URI_STEM = "uri-stem";
        public const string URI_QUERY = "uri-query";
    }
}
