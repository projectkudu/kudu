using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Globalization;

namespace Kudu.Core.AnalyticsDataLayer.Cookies
{


    class CookieParser
    {
        public struct Cookie
        {
            private string key, value;
            public Cookie(string key, string value)
            {
                this.key = key;
                this.value = value;
            }

            public string Key { get { return key; } }
            public string Value { get { return value; } }
        }

        //Generating without the parameter bases its seed off the system clock, its time-dependent therefore producing different sequences of values
        public static Random random = new Random();
        //static int number = 10;
        /**
         * <summary>
         *  use this method to extract a string representation of cookies
         *  and return a collection of cookies that came with the response
         * </summary>
         * <param name="stringCookieRepresentation"/>
         * <returns>CookieCollection given the string representation of cookies</returns>
         * */
        public Dictionary<string,string> ExtractServerHeaderResponseCookies(String stringCookieRepresentation)
        {
            if (stringCookieRepresentation.Equals("-"))
            {
                return null;
            }
            Dictionary<string, string> cookieCollection = new Dictionary<string,string>();
            //ARRAffinity=98db677339a86800132e1ea32fda737c9a700beb6ff89db0d28471b803c34fbc;+d4dad6935f632ac35975e3001dc7bbe8=fbqa6mm6s3a7okha8qs47uet66;+WAWebSiteSID=57a47b72403b4896be6b18e9583f381a
            string[] cookies = stringCookieRepresentation.Split(new Char[] { ';' });
            foreach (string stringCookie in cookies)
            {
                Cookie tempCookie = CreateCookie(stringCookie);
                if (tempCookie.Key != null && tempCookie.Value != null)
                {
                    //Trace.WriteLine(tempCookie.Key);
                    cookieCollection.Add(tempCookie.Key, tempCookie.Value);
                }
            }
            return cookieCollection;
        }

        private Cookie CreateCookie(String stringCookie)
        {
            //the components in cookies are delited by '=' to denote name and value pairs
            String[] cookieComponents = stringCookie.Split('=');
            try
            {
                string name = cookieComponents[0];
                string value = cookieComponents[1];
                return new Cookie(name, value);
            }
            catch (IndexOutOfRangeException)
            {
                return new Cookie(null,null);
            }
        }
    }
}
