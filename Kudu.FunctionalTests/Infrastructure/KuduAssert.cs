using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Kudu.Client;
using Kudu.Client.Infrastructure;
using Kudu.Core;
using Kudu.Core.Deployment;
using Kudu.TestHarness;
using Xunit;

namespace Kudu.FunctionalTests.Infrastructure
{
    public static class KuduAssert
    {
        public const string DefaultPageContent = "is up and running";

        public static T ThrowsUnwrapped<T>(Action action) where T : Exception
        {
            var ex = Assert.Throws<AggregateException>(() => action());
            var baseEx = ex.GetBaseException();
            Assert.IsAssignableFrom<T>(baseEx);
            return (T)baseEx;
        }

        public static async Task<T> ThrowsUnwrappedAsync<T>(Func<Task> action) where T : Exception
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                Assert.IsAssignableFrom<T>(ex);
                return (T)ex;
            }

            throw new Exception("Not throw, expected: " + typeof(T).Name);
        }

        public static Exception ThrowsMessage(string expected, Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Assert.Contains(expected, ex.Message);
                return ex;
            }

            throw new Exception("Not throw, expected: " + expected);
        }

        public static void VerifyUrl(Uri url, ICredentials cred, params string[] contents)
        {
            VerifyUrl(url.ToString(), cred, contents);
        }

        public static void VerifyUrl(string url, ICredentials cred, params string[] contents)
        {
            HttpClient client = HttpClientHelper.CreateClient(url, cred);
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Kudu-Test", "1.0"));
            var response = client.GetAsync(url).Result.EnsureSuccessful();

            if (contents.Length > 0)
            {
                var responseBody = response.Content.ReadAsStringAsync().Result;
                foreach (var content in contents)
                {
                    Assert.Contains(content, responseBody, StringComparison.Ordinal);
                }
            }
        }

        public static void VerifyUrl(string url, string content = null, HttpStatusCode statusCode = HttpStatusCode.OK,
            string httpMethod = "GET", string jsonPayload = "", ICredentials credentials = null)
        {
            VerifyUrlAsync(url, content, statusCode, httpMethod, jsonPayload).Wait();
        }

        public static async Task VerifyUrlAsync(string url, string content = null, HttpStatusCode statusCode = HttpStatusCode.OK, 
            string httpMethod = "GET", string jsonPayload = "", ICredentials credentials = null)
        {
            using (var client = new HttpClient(HttpClientHelper.CreateClientHandler(url, credentials)))
            {
                client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Kudu-Test", "1.0"));
                HttpResponseMessage response = null;
                if (String.Equals(httpMethod, "POST"))
                {
                    response = await client.PostAsync(url, new StringContent(jsonPayload, Encoding.UTF8, "application/json"));
                }
                else
                {
                    response = await client.GetAsync(url);
                }
                string responseBody = await response.Content.ReadAsStringAsync();

                Assert.True(statusCode == response.StatusCode,
                    String.Format("For {0}, Expected Status Code: {1} Actual Status Code: {2}. \r\n Response: {3}", url, statusCode, response.StatusCode, responseBody));

                if (content != null)
                {
                    Assert.Contains(content, responseBody, StringComparison.Ordinal);
                }
            }
        }

        /// <summary>
        /// Verifies if a text appears in the trace file.
        /// </summary>
        public static async Task VerifyTraceAsync(ApplicationManager manager, string text)
        {
            string trace = await manager.VfsManager.ReadAllTextAsync("LogFiles/Git/trace/trace.xml");

            // Since our trace files accumulates traces from multiple tests, we'll identify the start of the current test by looking for the call to delete the web root
            // and then look for the text starting at that point
            int index = trace.LastIndexOf("/scm/?deleteWebRoot=1", StringComparison.OrdinalIgnoreCase);
            index = Math.Max(0, index);

            Assert.Contains(text, trace.Substring(index));
        }

        public static void VerifyLogOutput(ApplicationManager appManager, string id, params string[] expectedMatches)
        {
            var allEntries = GetLogEntries(appManager, id);
            foreach (var expectedMatch in expectedMatches)
            {
                Assert.True(allEntries.Any(e => e.Message.Contains(expectedMatch)), "Didn't find '{0}' in log output".FormatInvariant(expectedMatch));
            }
        }

        public static void VerifyLogOutputWithUnexpected(ApplicationManager appManager, string id, params string[] unexpectedMatches)
        {
            var allEntries = GetLogEntries(appManager, id);
            Assert.True(unexpectedMatches.All(match => allEntries.All(e => !e.Message.Contains(match))));
        }

        private static List<LogEntry> GetLogEntries(ApplicationManager appManager, string id)
        {
            var entries = appManager.DeploymentManager.GetLogEntriesAsync(id).Result.ToList();
            Assert.True(entries.Count > 0);
            var allDetails = entries.Where(e => e.DetailsUrl != null)
                                    .AsParallel()
                                    .SelectMany(e => appManager.DeploymentManager.GetLogEntryDetailsAsync(id, e.Id).Result)
                                    .ToList();
            
            return entries.Concat(allDetails).ToList();
        }

        public static void Match(string pattern, string actual, string message = null)
        {
            Assert.True(Regex.IsMatch(actual, pattern), String.Format("{0}\r\npattern: {1}\r\nactual: {2}\r\n", message, pattern, actual));
        }

        public static void MatchAny(IEnumerable<string> patterns, string actual, string message = null)
        {
            GenericAnyAssert(patterns, (p) => Regex.IsMatch(actual, p), actual, message);
        }

        public static void EqualsAny(IEnumerable<string> options, string actual, string message = null)
        {
            GenericAnyAssert(options, (o) => string.Equals(actual, o), actual, message);
        }

        public static void ContainsAny(IEnumerable<string> options, string actual, string message = null)
        {
            GenericAnyAssert(options, (o) => actual.Contains(o), actual, message);
        }

        private static void GenericAnyAssert<T>(IEnumerable<T> options, Func<T, bool> assertFunc, T actual, string message = null)
        {
            Assert.True(options.Any(o => assertFunc(o)),
                        string.Format("{0}\r\noptions: {1}\r\nactual: {2}\r\n",
                                      message,
                                      options.Select(o => o.ToString()).Aggregate((a, b) => string.Format("{0}, {1}", a, b)),
                                      actual)
                       );
        }
    }
}
