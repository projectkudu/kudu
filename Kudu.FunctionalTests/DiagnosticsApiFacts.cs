using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using Kudu.Client;
using Kudu.TestHarness;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Kudu.FunctionalTests
{
    public class DiagnosticsApiFacts
    {
        [Fact]
        public void ConstructorTest()
        {
            string repositoryName = "Mvc3Application";
            string appName = KuduUtils.GetRandomWebsiteName("ConstructorTest");

            using (var repo = Git.CreateLocalRepository(repositoryName))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    using (HttpClient client = NewHttpClient(appManager))
                    {
                        HttpResponseMessage response = client.GetAsync("diagnostics/settings").Result.EnsureSuccessful();
                        using (var reader = new JsonTextReader(new StreamReader(response.Content.ReadAsStreamAsync().Result)))
                        {
                            JObject json = (JObject)JToken.ReadFrom(reader);
                            Assert.Equal(0, json.Count);
                        }
                    }

                    using (HttpClient client = NewHttpClient(appManager))
                    {
                        var ex = Assert.Throws<HttpRequestException>(() => client.GetAsync("diagnostics/settings/trace_level").Result.EnsureSuccessful());
                        Assert.Contains("404", ex.Message);
                    }
                });
            }
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
            string appName = KuduUtils.GetRandomWebsiteName("SetGetDeleteValue");

            using (var repo = Git.CreateLocalRepository(repositoryName))
            {
                ApplicationManager.Run(appName, appManager =>
                {
                    // set values
                    using (HttpClient client = NewHttpClient(appManager))
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
                    using (HttpClient client = NewHttpClient(appManager))
                    {
                        KeyValuePair<string, string> deleted = values[1];
                        client.DeleteAsync("diagnostics/settings/" + deleted.Key).Result.EnsureSuccessful();
                        values = values.Where(p => p.Key != deleted.Key).ToArray();
                    }

                    // update value
                    using (HttpClient client = NewHttpClient(appManager))
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

        private void VerifyValues(ApplicationManager appManager, params KeyValuePair<string, string>[] values)
        {
            using (HttpClient client = NewHttpClient(appManager))
            {
                HttpResponseMessage response = client.GetAsync("diagnostics/settings").Result.EnsureSuccessful();
                using (var reader = new JsonTextReader(new StreamReader(response.Content.ReadAsStreamAsync().Result)))
                {
                    JObject json = (JObject)JToken.ReadFrom(reader);
                    Assert.Equal(values.Length, json.Count);
                    foreach (KeyValuePair<string, string> value in values)
                    {
                        Assert.Equal(value.Value, json[value.Key].Value<string>());
                    }
                }
            }

            foreach (KeyValuePair<string, string> value in values)
            {
                using (HttpClient client = NewHttpClient(appManager))
                {
                    if (value.Value != null)
                    {
                        HttpResponseMessage response = client.GetAsync("diagnostics/settings/" + value.Key).Result.EnsureSuccessful();
                        var result = response.Content.ReadAsStringAsync().Result;
                        Assert.Equal(value.Value, result.Trim('\"'));
                    }
                    else
                    {
                        var ex = Assert.Throws<HttpRequestException>(() => client.GetAsync("diagnostics/settings/" + value.Key).Result.EnsureSuccessful());
                        Assert.Contains("404", ex.Message);
                    }
                }
            }
        }

        private HttpClient NewHttpClient(ApplicationManager appManager)
        {
            var client = new HttpClient(new HttpClientHandler()
            {
                Credentials = appManager.DeploymentManager.Credentials
            });

            client.BaseAddress = new Uri(appManager.ServiceUrl);

            return client;
        }
    }
}