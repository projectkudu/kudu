using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Kudu.Core.AnalyticsParser.CookieParser
{

    class CookieParser
    {
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
        public CookieCollection ExtractServerHeaderResponseCookies(String stringCookieRepresentation)
        {
            CookieCollection cookieCollection = new CookieCollection();
            //each cookie is delited by commas
            string[] cookies = stringCookieRepresentation.Split(new Char[] {','});
            //after this split it looks like:
            /*
             * ARRAffinity=8861359470be3744646f62d7260db44d8404c9e9ed260ca027274cd5ae097076;Path=/;Domain=t-hawkf-testing.azurewebsites.net
               d4dad6935f632ac35975e3001dc7bbe8=a5fn21iqeh639330gnns5uqaf4; path=/
               WAWebSiteSID=ede9cf16764e4c4cb1769bbcf8df36fe; Path=/; HttpOnly
             * */
            String pattern = "\\s+"; // one or more whitespace characters
            String replacement = String.Empty;
            Regex rgx = new Regex(pattern);
            foreach (string stringCookie in cookies)
            {
                //remove all whitespaces and add to the cookie collection
                String result = rgx.Replace(stringCookie, replacement);
                String[] cookieComponents = result.Split(';');
                Cookie tempCookie = CreateCookie(cookieComponents);
                cookieCollection.Add(tempCookie);
            }
            return cookieCollection;
        }

        private Cookie CreateCookie(String[] cookieComponenets)
        {
            Cookie cookie = new Cookie();
            const string reservedWordPath = "Path";
            const string reservedWordDomain = "Domain";
            const string reservedWordHttpOnly = "HttpOnly";
            //iterate through the array of cookie components and build the cookie until the end of the array and then return that cookie
            foreach (string component in cookieComponenets)
            {
                //the components in cookies are delited by '=' to denote name and value pairs, except for HttpOnly and Secure
                String[] nameValue = component.Split('=');
                //kind of pattern match with the Path, Domain, HttpOnly, and others to create the user cookie properly
                //Trace.WriteLine(nameValue[0]);
                if (string.Compare(nameValue[0], reservedWordPath, true) == 0)
                {
                    cookie.Path = nameValue[1];
                }
                else if (string.Compare(nameValue[0], reservedWordDomain, true) == 0)
                {
                    cookie.Domain = nameValue[1];
                }
                else if (string.Compare(nameValue[0], reservedWordHttpOnly, true) == 0)
                {
                    cookie.HttpOnly = true;
                }
                else
                {
                    try
                    {
                        cookie.Name = nameValue[0];
                        cookie.Value = nameValue[1];
                    }
                    catch (IndexOutOfRangeException)
                    {
                        cookie.Name = "null";
                        cookie.Value = "null";
                    }
                }
            }
            return cookie;
        }
    }
}
