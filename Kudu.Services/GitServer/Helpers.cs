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
    using System.Diagnostics;
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

        public static void AddSafeDirectoryConfigIfNotExist()
        {
            ProcessStartInfo processInfo = new ProcessStartInfo("git.exe", string.Format("config --global --get-all safe.directory"));
            processInfo.UseShellExecute = false;
            processInfo.RedirectStandardInput = true;
            processInfo.RedirectStandardError = true;
            processInfo.RedirectStandardOutput = true;

            StringBuilder error = new StringBuilder();
            DataReceivedEventHandler stderrHandler = (object sender, DataReceivedEventArgs e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    error.Append(e.Data);
                }
            };

            var process = Process.Start(processInfo);
            var processName = process.ProcessName;
            var processId = process.Id;

            string output = process.StandardOutput.ReadToEnd();

            Console.WriteLine(output);
            process.ErrorDataReceived += stderrHandler;
            process.BeginErrorReadLine();

            // waiting for 10 seconds
            process.WaitForExit(10000);

            if(process.ExitCode != 0 || error.Length != 0)
            {
                throw new Exception($"Process exited with {process.ExitCode} and error: {error}");
            }
            if (!string.IsNullOrWhiteSpace(output) && output.IndexOf('*') == -1)
            {
                ProcessStartInfo addSafeDirectoryProcessInfo = new ProcessStartInfo("git.exe", "config --global --add safe.directory *");
                addSafeDirectoryProcessInfo.UseShellExecute = false;
                addSafeDirectoryProcessInfo.RedirectStandardInput = true;
                addSafeDirectoryProcessInfo.RedirectStandardError = true;
                addSafeDirectoryProcessInfo.RedirectStandardOutput = true;

                StringBuilder addError = new StringBuilder();
                DataReceivedEventHandler errHandler = (object sender, DataReceivedEventArgs e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                    {
                        addError.Append(e.Data);
                    }
                };

                var addprocess = Process.Start(addSafeDirectoryProcessInfo);
                addprocess.ErrorDataReceived += errHandler;
                addprocess.BeginErrorReadLine();
                addprocess.WaitForExit(10000);
                if (process.ExitCode != 0 || addError.Length != 0)
                {
                    throw new Exception($"Process exited with {process.ExitCode} and error: {addError}");
                }
            }
        }
    }
}
