using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Kudu.Client;
using Kudu.Client.Infrastructure;
using Kudu.Contracts.Diagnostics;
using Kudu.Core.Infrastructure;
using Kudu.FunctionalTests.Infrastructure;
using Kudu.Services.Diagnostics;
using Kudu.TestHarness;
using Kudu.TestHarness.Xunit;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Kudu.FunctionalTests
{
    [KuduXunitTestClass]
    public class DiagnosticsApiFacts
    {
        [Fact]
        public void ConstructorTest()
        {
            string repositoryName = "Mvc3Application";
            string appName = "ConstructorTest";

            using (var repo = Git.CreateLocalRepository(repositoryName))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    using (HttpClient client = HttpClientHelper.CreateClient(appManager.ServiceUrl, appManager.DeploymentManager.Credentials))
                    {
                        HttpResponseMessage response = client.GetAsync("diagnostics/settings").Result.EnsureSuccessful();
                        using (var reader = new JsonTextReader(new StreamReader(response.Content.ReadAsStreamAsync().Result)))
                        {
                            JObject json = (JObject)JToken.ReadFrom(reader);
                            Assert.Equal(6, json.Count);
                        }
                    }

                    using (HttpClient client = HttpClientHelper.CreateClient(appManager.ServiceUrl, appManager.DeploymentManager.Credentials))
                    {
                        var ex = Assert.Throws<HttpUnsuccessfulRequestException>(() => client.GetAsync("diagnostics/settings/trace_level").Result.EnsureSuccessful());
                        Assert.Equal(HttpStatusCode.NotFound, ex.ResponseMessage.StatusCode);
                    }
                });
            }
        }

        [Fact]
        public async Task ProcessApiTests()
        {
            string appName = "ProcessApiTests";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                // Test current process
                var process = await appManager.ProcessManager.GetCurrentProcessAsync();
                int currentId = process.Id;
                var currentUser = process.UserName;
                DateTime startTime = process.StartTime;
                Assert.NotNull(process);
                Assert.Contains("w3wp", process.Name);
                Assert.Contains("/api/processes/" + currentId, process.Href.AbsoluteUri);

                // Test get process by id
                process = await appManager.ProcessManager.GetProcessAsync(currentId);
                Assert.NotNull(process);
                Assert.Contains("w3wp", process.Name);
                Assert.Contains("/api/processes/" + currentId, process.Href.AbsoluteUri);

                // Test get not running process id
                var notfound = await KuduAssert.ThrowsUnwrappedAsync<HttpUnsuccessfulRequestException>(() => appManager.ProcessManager.GetProcessAsync(99999));
                Assert.Equal(HttpStatusCode.NotFound, notfound.ResponseMessage.StatusCode);
                Assert.Contains("is not running", notfound.ResponseMessage.ExceptionMessage);

                // Test process list
                var processes = await appManager.ProcessManager.GetProcessesAsync();
                Assert.True(processes.Count() >= 1);
                Assert.True(processes.Any(p => p.Id == currentId));
                Assert.True(processes.All(p => p.UserName == currentUser));

                // Test all-users process list
                var allProcesses = await appManager.ProcessManager.GetProcessesAsync(allUsers: true);
                Assert.True(allProcesses.Count() >= processes.Count());
                Assert.True(allProcesses.Any(p => p.Id == currentId));
                Assert.True(allProcesses.Any(p => p.UserName == currentUser));

                // Test process dumps
                foreach (var format in new[] { "raw", "zip", "diagsession" })
                {
                    if (format != "diagsession")
                    {
                        TestTracer.Trace("Test minidump format={0}", format);
                        using (var stream = new MemoryStream())
                        {
                            using (var minidump = await appManager.ProcessManager.MiniDump(format: format))
                            {
                                Assert.NotNull(minidump);
                                await minidump.CopyToAsync(stream);
                            }
                            TestTracer.Trace("Test minidump lenth={0}", stream.Length);
                            Assert.True(stream.Length > 0);
                        }
                    }

                    if (ProcessExtensions.SupportGCDump)
                    {
                        TestTracer.Trace("Test gcdump format={0}", format);
                        using (var stream = new MemoryStream())
                        {
                            using (var gcdump = await appManager.ProcessManager.GCDump(format: format))
                            {
                                Assert.NotNull(gcdump);
                                await gcdump.CopyToAsync(stream);
                            }
                            TestTracer.Trace("Test gcdump lenth={0}", stream.Length);
                            Assert.True(stream.Length > 0);
                        }
                    }
                }

                //Test Handles
                process = await appManager.ProcessManager.GetCurrentProcessAsync();
                Assert.NotNull(process);
                Assert.True(
                    process.OpenFileHandles.Any(
                        h => h.IndexOf("kudu.core.dll", StringComparison.InvariantCultureIgnoreCase) != -1));

                //Test Modules
                process = await appManager.ProcessManager.GetCurrentProcessAsync();
                Assert.NotNull(process);
                Assert.True(
                    process.Modules.Any(h => h.FileName.Equals("ntdll.dll", StringComparison.InvariantCultureIgnoreCase)));

                // Test kill process
                await KuduAssert.ThrowsUnwrappedAsync<HttpRequestException>(() => appManager.ProcessManager.KillProcessAsync(currentId));
                HttpUtils.WaitForSite(appManager.SiteUrl, delayBeforeRetry: 10000);
                process = await appManager.ProcessManager.GetCurrentProcessAsync();
                Assert.NotEqual(startTime, process.StartTime);
            });
        }

        [Fact]
        public void SetGetDeleteValue()
        {
            var values = new[]
            {
                new KeyValuePair<string, string>(Guid.NewGuid().ToString(), Guid.NewGuid().ToString()),
                new KeyValuePair<string, string>(Guid.NewGuid().ToString(), String.Empty),
                new KeyValuePair<string, string>(Guid.NewGuid().ToString(), null)
            };

            string repositoryName = "Mvc3Application";
            string appName = "SetGetDeleteValue";

            using (var repo = Git.CreateLocalRepository(repositoryName))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // set values
                    using (HttpClient client = HttpClientHelper.CreateClient(appManager.ServiceUrl, appManager.DeploymentManager.Credentials))
                    {
                        JObject json = new JObject();
                        foreach (KeyValuePair<string, string> value in values)
                        {
                            json[value.Key] = value.Value;
                        }
                        client.PostAsJsonAsync("diagnostics/settings", json).Result.EnsureSuccessful();
                    }

                    // verify values
                    VerifyValues(appManager, values);

                    // delete value
                    using (HttpClient client = HttpClientHelper.CreateClient(appManager.ServiceUrl, appManager.DeploymentManager.Credentials))
                    {
                        KeyValuePair<string, string> deleted = values[1];
                        client.DeleteAsync("diagnostics/settings/" + deleted.Key).Result.EnsureSuccessful();
                        values = values.Where(p => p.Key != deleted.Key).ToArray();
                    }

                    // update value
                    using (HttpClient client = HttpClientHelper.CreateClient(appManager.ServiceUrl, appManager.DeploymentManager.Credentials))
                    {
                        KeyValuePair<string, string> updated = values[0] = new KeyValuePair<string, string>(values[0].Key, Guid.NewGuid().ToString());
                        JObject json = new JObject();
                        json[updated.Key] = updated.Value;
                        client.PostAsJsonAsync("diagnostics/settings", json).Result.EnsureSuccessful();
                    }

                    // verify values
                    VerifyValues(appManager, values);
                });
            }
        }

        [Fact]
        public void DiagnosticsSettingsExpectedValuesReturned()
        {
            const string expectedEmptyResponse =
                "{\"AzureDriveEnabled\":false,\"AzureDriveTraceLevel\":\"Error\"," +
                "\"AzureTableEnabled\":false,\"AzureTableTraceLevel\":\"Error\"," +
                "\"AzureBlobEnabled\":false,\"AzureBlobTraceLevel\":\"Error\"}";

            const string settingsContentWithNumbers = "{AzureDriveEnabled: true,AzureDriveTraceLevel: \"4\"}";
            const string expectedNumbersResponse =
                "{\"AzureDriveEnabled\":true,\"AzureDriveTraceLevel\":\"Warning\"," +
                "\"AzureTableEnabled\":false,\"AzureTableTraceLevel\":\"Error\"," +
                "\"AzureBlobEnabled\":false,\"AzureBlobTraceLevel\":\"Error\"}";

            const string settingsContentWithNulls = "{AzureDriveEnabled: null,AzureDriveTraceLevel: \"4\"}";

            const string repositoryName = "Mvc3Application";
            const string appName = "DiagnosticsSettingsExpectedValuesReturned";

            using (var repo = Git.CreateLocalRepository(repositoryName))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // verify values
                    using (HttpClient client = HttpClientHelper.CreateClient(appManager.ServiceUrl, appManager.DeploymentManager.Credentials))
                    {
                        // Empty settings.json should return default values
                        string responseContent = DownloadDiagnosticsSettings(client);
                        Assert.Equal(expectedEmptyResponse, responseContent);

                        // Expected string value for enums
                        appManager.VfsManager.WriteAllText("site/diagnostics/settings.json", settingsContentWithNumbers);
                        responseContent = DownloadDiagnosticsSettings(client);
                        Assert.Equal(expectedNumbersResponse, responseContent);

                        // Invalid json we expect default values
                        appManager.VfsManager.WriteAllText("site/diagnostics/settings.json", settingsContentWithNulls);
                        responseContent = DownloadDiagnosticsSettings(client);
                        Assert.Equal(expectedEmptyResponse, responseContent);
                    }
                });
            }
        }

        private string DownloadDiagnosticsSettings(HttpClient client)
        {
            HttpResponseMessage response = client.GetAsync("diagnostics/settings").Result.EnsureSuccessful();
            return response.Content.ReadAsStringAsync().Result;
        }

        private void VerifyValues(ApplicationManager appManager, params KeyValuePair<string, string>[] values)
        {
            using (HttpClient client = HttpClientHelper.CreateClient(appManager.ServiceUrl, appManager.DeploymentManager.Credentials))
            {
                HttpResponseMessage response = client.GetAsync("diagnostics/settings").Result.EnsureSuccessful();
                using (var reader = new JsonTextReader(new StreamReader(response.Content.ReadAsStreamAsync().Result)))
                {
                    JObject json = (JObject)JToken.ReadFrom(reader);
                    Assert.Equal(values.Length + 6, json.Count);
                    foreach (KeyValuePair<string, string> value in values)
                    {
                        Assert.Equal(value.Value, json[value.Key].Value<string>());
                    }
                }
            }

            foreach (KeyValuePair<string, string> value in values)
            {
                using (HttpClient client = HttpClientHelper.CreateClient(appManager.ServiceUrl, appManager.DeploymentManager.Credentials))
                {
                    if (value.Value != null)
                    {
                        HttpResponseMessage response = client.GetAsync("diagnostics/settings/" + value.Key).Result.EnsureSuccessful();
                        var result = response.Content.ReadAsStringAsync().Result;
                        Assert.Equal(value.Value, result.Trim('\"'));
                    }
                    else
                    {
                        var ex = Assert.Throws<HttpUnsuccessfulRequestException>(() => client.GetAsync("diagnostics/settings/" + value.Key).Result.EnsureSuccessful());
                        Assert.Equal(HttpStatusCode.NotFound, ex.ResponseMessage.StatusCode);
                    }
                }
            }
        }

        [Fact]
        public async Task DiagnosticsDumpTests()
        {
            string appName = "DiagnosticsDumpTests";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                string path = String.Format("dump?marker={0}", Guid.NewGuid());
                using (HttpClient client = HttpClientHelper.CreateClient(appManager.ServiceUrl, appManager.DeploymentManager.Credentials))
                {
                    using (var zipStream = new MemoryStream())
                    {
                        using (var dump = await client.GetStreamAsync(path))
                        {
                            Assert.NotNull(dump);
                            await dump.CopyToAsync(zipStream);
                        }
                        TestTracer.Trace("zipStream lenth={0}", zipStream.Length);
                        Assert.True(zipStream.Length > 0);

                        zipStream.Position = 0;
                        using (var targetStream = new MemoryStream())
                        {
                            ZipUtils.Unzip(zipStream, targetStream);
                            TestTracer.Trace("targetStream lenth={0}", targetStream.Length);
                            Assert.True(targetStream.Length > 0);
                        }
                    }
                }

                // Ensure trace
                string trace = await appManager.VfsManager.ReadAllTextAsync("LogFiles/kudu/trace/");
                Assert.Contains("_GET_dump_200_", trace, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("s.xml", trace, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("_GET_api-vfs-LogFiles-kudu-trace_pending.xml", trace, StringComparison.OrdinalIgnoreCase);

                // Test runtime object by checking for one Node version
                RuntimeInfo runtimeInfo = await appManager.RuntimeManager.GetRuntimeInfo();
                Assert.True(runtimeInfo.NodeVersions.Any(dict => dict["version"] == "0.8.2"));
            });
        }

        [Fact]
        public async Task TestRecentLogEntries()
        {
            string appName = "TestRecentLogEntries";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                IList<ApplicationLogEntry> results = null;

                using (var localRepo = Git.Clone("LogTester"))
                {
                    appManager.GitDeploy(localRepo.PhysicalPath);
                }

                await appManager.CommandExecutor.ExecuteCommand("rm *.txt", @"LogFiles/Application");
                results = await appManager.LogFilesManager.GetRecentLogEntriesAsync(10);

                // All the log files have been deleted so this API should return an empty array.
                Assert.Equal(0, results.Count);

                var logFile =
@"2013-12-06T00:29:20  PID[20108] Information this is a log
2013-12-06T00:29:21  PID[20108] Warning     this is a warning
that spans
several lines
2013-12-06T00:29:22  PID[20108] Error       this is an error
";
                await WriteLogText(appManager.SiteUrl, @"LogFiles\Application\test-1.txt", logFile);
                await WriteLogText(appManager.SiteUrl, @"LogFiles\Application\test-2.txt", logFile);

                // Verify that the log files were written
                var dirResult = await appManager.CommandExecutor.ExecuteCommand("ls -1 | wc -l", @"LogFiles/Application");
                Assert.Equal(2, int.Parse(dirResult.Output));

                // Verify that the log entries have appeared in the correct order
                results = await appManager.LogFilesManager.GetRecentLogEntriesAsync(10);
                Assert.Equal(6, results.Count);
                AssertLogEntry(results[0], "2013-12-06T00:29:22+00:00", "Error", "this is an error");
                AssertLogEntry(results[1], "2013-12-06T00:29:22+00:00", "Error", "this is an error");
                AssertLogEntry(results[2], "2013-12-06T00:29:21+00:00", "Warning", "this is a warning\r\nthat spans\r\nseveral lines");
                AssertLogEntry(results[3], "2013-12-06T00:29:21+00:00", "Warning", "this is a warning\r\nthat spans\r\nseveral lines");
                AssertLogEntry(results[4], "2013-12-06T00:29:20+00:00", "Information", "this is a log");
                AssertLogEntry(results[5], "2013-12-06T00:29:20+00:00", "Information", "this is a log");
            });
        }

        private static void AssertLogEntry(ApplicationLogEntry entry, string timeStamp, string level, string message)
        {
            Assert.Equal(DateTimeOffset.Parse(timeStamp, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal), entry.TimeStamp);
            Assert.Equal(level, entry.Level);
            Assert.Equal(message, entry.Message);
        }

        private static async Task WriteLogText(string siteUrl, string filePath, string log)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Kudu-Test", "1.0"));
                var content = new FormUrlEncodedContent(new Dictionary<string, string>() {
                    { "path", filePath },
                    { "content", log }
                });

                HttpResponseMessage response = await client.PostAsync(siteUrl, content);
                string responseBody = await response.Content.ReadAsStringAsync();

                Assert.True(response.StatusCode == HttpStatusCode.OK,
                    String.Format("For {0}, Expected Status Code: {1} Actual Status Code: {2}. \r\n Response: {3}", siteUrl, 200, response.StatusCode, responseBody));
            }
        }

        [Fact]
        public async Task Error404UnknownRouteTests()
        {
            string appName = "Error404UnknownRouteTests";

            await ApplicationManager.RunAsync(appName, async appManager =>
            {
                using (var client = HttpClientHelper.CreateClient(appManager.ServiceUrl, appManager.DeploymentManager.Credentials))
                {
                    var path = "/api/badroute";
                    using (var response = await client.GetAsync(path))
                    {
                        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

                        var content = await response.Content.ReadAsStringAsync();

                        // assert
                        Assert.True(content.Contains("No route registered for"));
                        Assert.True(content.Contains(path));
                    }
                }
            });
        }
    }
}