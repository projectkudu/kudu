using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Kudu.Client;
using Kudu.Core;
using Kudu.Core.Deployment;
using Kudu.Core.Hooks;
using Kudu.TestHarness;
using Kudu.TestHarness.Xunit;
using Newtonsoft.Json;
using Xunit;

namespace Kudu.FunctionalTests
{
    [KuduXunitTestClass]
    public class WebHooksTests
    {
        [Fact]
        public async Task SubscribedWebHooksShouldBeCalledPostDeployment()
        {
            string testName = "SubscribedWebHooksShouldBeCalledPostDeployment";
            var expectedHookAddresses = new List<string>();
            string hook1 = "hookCalled/1";
            string hook2 = "hookCalled/2";

            using (new LatencyLogger(testName))
            {
                await ApplicationManager.RunAsync("HookSite" + testName, async hookAppManager =>
                {
                    using (var hookAppRepository = Git.Clone("NodeWebHookTest"))
                    {
                        string hookAddress1 = hookAppManager.SiteUrl + hook1;
                        string hookAddress2 = hookAppManager.SiteUrl + hook2;

                        WebHook webHookAdded1 = await SubscribeWebHook(hookAppManager, hookAddress1, 1);

                        GitDeployApp(hookAppManager, hookAppRepository);

                        expectedHookAddresses.Add(hook1);
                        await VerifyWebHooksCall(expectedHookAddresses, hookAppManager, DeployStatus.Success.ToString(), hookAppRepository.CurrentId);

                        WebHook webHookAdded2 = await SubscribeWebHook(hookAppManager, hookAddress2, 2);

                        TestTracer.Trace("Redeploy to allow web hooks to be called");
                        await hookAppManager.DeploymentManager.DeployAsync(hookAppRepository.CurrentId);

                        expectedHookAddresses.Add(hook2);
                        await VerifyWebHooksCall(expectedHookAddresses, hookAppManager, DeployStatus.Success.ToString(), hookAppRepository.CurrentId);

                        TestTracer.Trace("Make sure web hooks are called for failed deployments");
                        await hookAppManager.SettingsManager.SetValue("COMMAND", "thisIsAnErrorCommand");
                        await hookAppManager.DeploymentManager.DeployAsync(hookAppRepository.CurrentId);
                        await VerifyWebHooksCall(expectedHookAddresses, hookAppManager, DeployStatus.Failed.ToString());

                        await hookAppManager.SettingsManager.Delete("COMMAND");

                        TestTracer.Trace("Unsubscribe first hook");
                        await UnsubscribeWebHook(hookAppManager, webHookAdded1.Id, 1);

                        TestTracer.Trace("Redeploy to allow web hook to be called");
                        await hookAppManager.DeploymentManager.DeployAsync(hookAppRepository.CurrentId);

                        expectedHookAddresses.Remove(hook1);
                        await VerifyWebHooksCall(expectedHookAddresses, hookAppManager, DeployStatus.Success.ToString(), hookAppRepository.CurrentId);

                        TestTracer.Trace("Unsubscribe second hook");
                        await UnsubscribeWebHook(hookAppManager, webHookAdded2.Id, 0);

                        TestTracer.Trace("Redeploy to verify no web hook was called");
                        await hookAppManager.DeploymentManager.DeployAsync(hookAppRepository.CurrentId);

                        TestTracer.Trace("Verify web hook was not called");
                        expectedHookAddresses.Remove(hook2);
                        await VerifyWebHooksCall(expectedHookAddresses, hookAppManager, DeployStatus.Success.ToString(), hookAppRepository.CurrentId);
                    }
                });
            }
        }

        [Fact]
        public async Task SubscribedWebHooksShouldBeCalledWithPublish()
        {
            string testName = "SubscribedWebHooksShouldBeCalledWithPublish";
            var expectedHookAddresses = new List<string>();
            string hook1 = "hookCalled/1";
            string hook2 = "hookCalled/2";

            using (new LatencyLogger(testName))
            {
                await ApplicationManager.RunAsync("HookSite" + testName, async hookAppManager =>
                {
                    using (var hookAppRepository = Git.Clone("NodeWebHookTest"))
                    {
                        GitDeployApp(hookAppManager, hookAppRepository);

                        string hookAddress1 = hookAppManager.SiteUrl + hook1;
                        string hookAddress2 = hookAppManager.SiteUrl + hook2;

                        string customHookEventType = "CustomEvent";

                        WebHook webHookAdded1 = await SubscribeWebHook(hookAppManager, hookAddress1, 1, customHookEventType);
                        WebHook webHookAdded2 = await SubscribeWebHook(hookAppManager, hookAddress2, 2, customHookEventType);

                        var jsonObject = new
                        {
                            TestProperty = "mytest_property",
                            CustomProperty = "mycustom_property"
                        };

                        await hookAppManager.WebHooksManager.PublishEventAsync(customHookEventType, jsonObject);

                        expectedHookAddresses.Add(hook1);
                        expectedHookAddresses.Add(hook2);

                        await VerifyWebHooksCall(expectedHookAddresses, hookAppManager, jsonObject.TestProperty, jsonObject.CustomProperty);
                    }
                });
            }
        }

        [Fact]
        public async Task SubscribedWebHooksShouldFailOnErrors()
        {
            string testName = "SubscribedWebHooksShouldFailOnErrors";
            string hook = "hookCalled/1";

            using (new LatencyLogger(testName))
            {
                await ApplicationManager.RunAsync("HookSite" + testName, async hookAppManager =>
                {
                    string customHookEventType = "CustomEvent";

                    string hookAddress = hookAppManager.SiteUrl + hook;

                    await hookAppManager.WebHooksManager.SubscribeAsync(new WebHook(customHookEventType, hookAddress));

                    var thrownException = await Assert.ThrowsAsync<HttpUnsuccessfulRequestException>(async () =>
                    {
                        await hookAppManager.WebHooksManager.SubscribeAsync(new WebHook(customHookEventType + "_DifferentEvent", hookAddress));
                    });

                    Assert.Contains("Conflict", thrownException.Message);
                });
            }
        }

        private static void GitDeployApp(ApplicationManager hookAppManager, TestRepository hookAppRepository)
        {
            TestTracer.Trace("Deploy test app");
            hookAppManager.GitDeploy(hookAppRepository.PhysicalPath);
            var deploymentResults = hookAppManager.DeploymentManager.GetResultsAsync().Result.ToList();
            Assert.Equal(1, deploymentResults.Count);
            Assert.Equal(DeployStatus.Success, deploymentResults[0].Status);
        }

        private static async Task<WebHook> SubscribeWebHook(ApplicationManager hookAppManager, string hookAddress, int expectedHooksCount, string hookEventType = "PostDeployment")
        {
            TestTracer.Trace("Subscribe web hook to " + hookAddress);
            WebHook webHookAdded = await hookAppManager.WebHooksManager.SubscribeAsync(new WebHook(hookEventType, hookAddress));

            await VerifyWebHooksCount(hookAppManager, expectedHooksCount);
            await VerifyWebHook(hookAppManager, webHookAdded);

            return webHookAdded;
        }

        private static async Task UnsubscribeWebHook(ApplicationManager hookAppManager, string hookAddress, int expectedHooksCount)
        {
            TestTracer.Trace("Unsubscribe web hook " + hookAddress);
            await hookAppManager.WebHooksManager.UnsubscribeAsync(hookAddress);

            await VerifyWebHooksCount(hookAppManager, expectedHooksCount);
        }

        private static async Task VerifyWebHooksCount(ApplicationManager hookAppManager, int expectedHooksCount)
        {
            TestTracer.Trace("Verify web hook subscribed");
            IEnumerable<WebHook> webHooks = await hookAppManager.WebHooksManager.GetWebHooksAsync();
            Assert.Equal(expectedHooksCount, webHooks.Count());
        }

        private static async Task VerifyWebHook(ApplicationManager hookAppManager, WebHook expectedWebHook)
        {
            TestTracer.Trace("Verify web hook");
            WebHook actualWebHooks = await hookAppManager.WebHooksManager.GetWebHookAsync(expectedWebHook.Id);
            Assert.Equal(expectedWebHook.HookAddress, actualWebHooks.HookAddress);
            Assert.Equal(expectedWebHook.HookEventType, actualWebHooks.HookEventType);
        }

        private async Task VerifyWebHooksCall(IEnumerable<string> hookAddresses, ApplicationManager hookAppManager, params string[] expectedContents)
        {
            TestTracer.Trace("Verify web hook was called {0} times".FormatCurrentCulture(hookAddresses.Count()));

            string webHookCallResponse = await GetWebHookResponseAsync(hookAppManager.SiteUrl);

            string[] webHookResults = webHookCallResponse.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

            Assert.Equal(hookAddresses.Count(), webHookResults.Count());

            foreach (var hookAddress in hookAddresses)
            {
                bool found = false;

                foreach (var webHookResult in webHookResults)
                {
                    dynamic webHookResultObject = JsonConvert.DeserializeObject(webHookResult);
                    if (("/" + hookAddress) == (string)webHookResultObject.url)
                    {
                        var body = (string)webHookResultObject.body;
                        found = true;

                        // Make sure body json
                        JsonConvert.DeserializeObject(body);
                        foreach (var expectedContent in expectedContents)
                        {
                            Assert.Contains(expectedContent, body, StringComparison.OrdinalIgnoreCase);
                        }
                    }
                }

                Assert.True(found, "Web hook address {0} was not called".FormatCurrentCulture(hookAddress));
            }

            hookAppManager.VfsWebRootManager.Delete("result.txt");
        }

        private async Task<string> GetWebHookResponseAsync(string hookAddress)
        {
            using (var httpClient = new HttpClient())
            {
                var response = await httpClient.GetAsync(hookAddress);
                string responseContent = await response.Content.ReadAsStringAsync();

                TestTracer.Trace("Received response: {0}", responseContent);

                if (response.IsSuccessStatusCode)
                {
                    return responseContent;
                }

                return String.Empty;
            }
        }
    }
}