#region License

// Copyright 2010 Jeremy Skinner (http://www.jeremyskinner.co.uk)
//  
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
// 
// http://www.apache.org/licenses/LICENSE-2.0 
// 
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
// 
// The latest version of this file can be found at http://github.com/JeremySkinner/git-dot-aspx

// This file was modified from the one found in git-dot-aspx

#endregion

namespace Kudu.Services.GitServer {
    using System;
    using System.ComponentModel;
    using System.Web;
    using System.Web.Mvc;
    using System.Web.Routing;

    public static class Helpers {
        static readonly string version;

        static Helpers() {
            version = typeof(Helpers).Assembly.GetName().Version.ToString();
        }

        public static string Version {
            get { return version; }
        }

        public static string ProjectUrl(this UrlHelper urlHelper, string project) {
            return urlHelper.RouteUrl("project", new RouteValueDictionary(new { project }),
                                      urlHelper.RequestContext.HttpContext.Request.Url.Scheme,
                                      urlHelper.RequestContext.HttpContext.Request.Url.Host);
        }

        public static string With(this string format, params string[] args) {
            return String.Format(format, args);
        }

        public static void WriteNoCache(this HttpResponseBase response) {
            response.AddHeader("Expires", "Fri, 01 Jan 1980 00:00:00 GMT");
            response.AddHeader("Pragma", "no-cache");
            response.AddHeader("Cache-Control", "no-cache, max-age=0, must-revalidate");
        }

        public static void PktWrite(this HttpResponseBase response, string input, params object[] args) {
            input = String.Format(input, args);
            var toWrite = (input.Length + 4).ToString("x").PadLeft(4, '0') + input;
            response.Write(toWrite);
        }

        public static void PktFlush(this HttpResponseBase response) {
            response.Write("0000");
        }
    }
}
