using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Kudu.Core;
using Kudu.Core.Deployment;
using Kudu.Core.Hooks;
using Kudu.TestHarness;
using Newtonsoft.Json;
using Xunit;

namespace Kudu.FunctionalTests
{
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
                        await VerifyWebHooksCall(hookAddress1, expectedHookAddresses, hookAppRepository.CurrentId, hookAppManager);

                        WebHook webHookAdded2 = await SubscribeWebHook(hookAppManager, hookAddress2, 2);

                        TestTracer.Trace("Redeploy to allow web hooks to be called");
                        await hookAppManager.DeploymentManager.DeployAsync(hookAppRepository.CurrentId);

                        expectedHookAddresses.Add(hook2);
                        await VerifyWebHooksCall(hookAddress1, expectedHookAddresses, hookAppRepository.CurrentId, hookAppManager);

                        TestTracer.Trace("Unsubscribe first hook");
                        await UnsubscribeWebHook(hookAppManager, webHookAdded1.Id, 1);

                        TestTracer.Trace("Redeploy to allow web hook to be called");
                        await hookAppManager.DeploymentManager.DeployAsync(hookAppRepository.CurrentId);

                        expectedHookAddresses.Remove(hook1);
                        await VerifyWebHooksCall(hookAddress1, expectedHookAddresses, hookAppRepository.CurrentId, hookAppManager);

                        TestTracer.Trace("Unsubscribe second hook");
                        await UnsubscribeWebHook(hookAppManager, webHookAdded2.Id, 0);

                        TestTracer.Trace("Redeploy to verify no web hook was called");
                        await hookAppManager.DeploymentManager.DeployAsync(hookAppRepository.CurrentId);

                        TestTracer.Trace("Verify web hook was not called");
                        expectedHookAddresses.Remove(hook2);
                        await VerifyWebHooksCall(hookAddress1, expectedHookAddresses, hookAppRepository.CurrentId, hookAppManager);
                    }
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

        private static async Task<WebHook> SubscribeWebHook(ApplicationManager hookAppManager, string hookAddress, int expectedHooksCount)
        {
            TestTracer.Trace("Subscribe web hook to " + hookAddress);
            WebHook webHookAdded = await hookAppManager.WebHooksManager.SubscribeAsync(new WebHook(HookEventTypes.PostDeployment, hookAddress));

            await VerifyWebHooksCount(hookAppManager, expectedHooksCount);

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

        private async Task VerifyWebHooksCall(string siteAddress, IEnumerable<string> hookAddresses, string commitId, ApplicationManager hookAppManager)
        {
            TestTracer.Trace("Verify web hook was called {0} times".FormatCurrentCulture(hookAddresses.Count()));

            string webHookCallResponse = await GetWebHookResponseAsync(siteAddress);

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
                        found = true;
                        Assert.True(((string)webHookResultObject.body).Contains(DeployStatus.Success.ToString()), "Missing Success from body");
                        Assert.True(((string)webHookResultObject.body).Contains(commitId), "Missing commit id from body - " + commitId);
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
