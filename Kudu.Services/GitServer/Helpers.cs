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

namespace Kudu.Services.GitServer
{
    using System;
    using System.IO;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;

    public static class Helpers
    {
        public static string With(this string format, params string[] args)
        {
            return String.Format(format, args);
        }

        public static void WriteNoCache(this HttpResponseMessage response)
        {
            response.Content.Headers.Expires = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
            response.Headers.Pragma.Add(new NameValueHeaderValue("no-cache"));
            response.Headers.CacheControl = new CacheControlHeaderValue()
            {
                NoCache = true,
                MaxAge = TimeSpan.Zero,
                MustRevalidate = true
            };
        }

        public static void PktWrite(this Stream response, string input, params object[] args)
        {
            input = String.Format(input, args);
            var toWrite = (input.Length + 4).ToString("x").PadLeft(4, '0') + input;
            response.Write(Encoding.UTF8.GetBytes(toWrite), 0, Encoding.UTF8.GetByteCount(toWrite));
        }

        public static void PktFlush(this Stream response)
        {
            var toWrite = "0000";
            response.Write(Encoding.UTF8.GetBytes(toWrite), 0, Encoding.UTF8.GetByteCount(toWrite));
        }
    }
}
