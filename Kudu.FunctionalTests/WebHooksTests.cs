using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Kudu.Core.Deployment;
using Kudu.Core.Hooks;
using Kudu.TestHarness;
using Newtonsoft.Json;
using Xunit;

namespace Kudu.FunctionalTests
{
    public class WebHooksTests
    {
        // ASP.NET apps

        [Fact]
        public async Task SubscribedWebHooksShouldBeCalledPostDeployment()
        {
            string testName = "SubscribedWebHooksShouldBeCalledPostDeployment";

            using (new LatencyLogger(testName))
            {
                await ApplicationManager.RunAsync("HookSite" + testName, async hookAppManager =>
                {
                    using (var hookAppRepository = Git.Clone("NodeWebHookTest"))
                    {
                        string hookAddress1 = hookAppManager.SiteUrl + "hookCalled/1";

                        TestTracer.Trace("Subscribe web hook to " + hookAddress1);
                        await hookAppManager.WebHooksManager.SubscribeAsync(new WebHook(HookEventType.PostDeployment, hookAddress1));

                        TestTracer.Trace("Verify web hook subscribed");
                        IEnumerable<WebHook> webHooks = await hookAppManager.WebHooksManager.GetWebHooksAsync();
                        Assert.Equal(1, webHooks.Count());

                        TestTracer.Trace("Deploy test app");
                        hookAppManager.GitDeploy(hookAppRepository.PhysicalPath);
                        var deploymentResults = hookAppManager.DeploymentManager.GetResultsAsync().Result.ToList();
                        Assert.Equal(1, deploymentResults.Count);
                        Assert.Equal(DeployStatus.Success, deploymentResults[0].Status);

                        TestTracer.Trace("Verify web hook was called");
                        string webHookCallResponse = await GetWebHookResponseAsync(hookAddress1);

                        string[] results = webHookCallResponse.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        Assert.Equal(1, results.Length);

                        VerifyWebHookCall(results[0], "/hookCalled/1", hookAppRepository.CurrentId);

                        var hookAddress2 = hookAppManager.SiteUrl + "hookCalled/2";

                        TestTracer.Trace("Subscribe another web hook to " + hookAddress2);
                        await hookAppManager.WebHooksManager.SubscribeAsync(new WebHook(HookEventType.PostDeployment, hookAddress2));

                        TestTracer.Trace("Verify web hooks subscribed");
                        webHooks = await hookAppManager.WebHooksManager.GetWebHooksAsync();
                        Assert.Equal(2, webHooks.Count());

                        TestTracer.Trace("Redeploy to allow web hooks to be called");
                        await hookAppManager.DeploymentManager.DeployAsync(hookAppRepository.CurrentId);

                        TestTracer.Trace("Verify web hooks were called");
                        webHookCallResponse = await GetWebHookResponseAsync(hookAddress1);

                        results = webHookCallResponse.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        Assert.Equal(3, results.Length);

                        VerifyWebHookCall(results[1], "/hookCalled/1", hookAppRepository.CurrentId);
                        VerifyWebHookCall(results[2], "/hookCalled/2", hookAppRepository.CurrentId);

                        TestTracer.Trace("Unsubscribe first hook");
                        await hookAppManager.WebHooksManager.UnsubscribeAsync(hookAddress1);

                        TestTracer.Trace("Verify web hook was removed");
                        webHooks = await hookAppManager.WebHooksManager.GetWebHooksAsync();
                        Assert.Equal(1, webHooks.Count());

                        TestTracer.Trace("Redeploy to allow web hook to be called");
                        await hookAppManager.DeploymentManager.DeployAsync(hookAppRepository.CurrentId);

                        TestTracer.Trace("Verify only one web hook was called");
                        webHookCallResponse = await GetWebHookResponseAsync(hookAddress1);

                        results = webHookCallResponse.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        Assert.Equal(4, results.Length);

                        VerifyWebHookCall(results[3], "/hookCalled/2", hookAppRepository.CurrentId);

                        TestTracer.Trace("Unsubscribe second hook");
                        await hookAppManager.WebHooksManager.UnsubscribeAsync(hookAddress2);

                        TestTracer.Trace("Verify web hook was removed");
                        webHooks = await hookAppManager.WebHooksManager.GetWebHooksAsync();
                        Assert.Equal(0, webHooks.Count());

                        TestTracer.Trace("Redeploy to verify no web hook was called");
                        await hookAppManager.DeploymentManager.DeployAsync(hookAppRepository.CurrentId);

                        TestTracer.Trace("Verify web hook was not called");
                        webHookCallResponse = await GetWebHookResponseAsync(hookAddress1);

                        results = webHookCallResponse.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        Assert.Equal(4, results.Length);
                    }
                });
            }
        }

        private void VerifyWebHookCall(string webHookResult, string hookAddress, string commitId)
        {
            dynamic webHookResultObject = JsonConvert.DeserializeObject(webHookResult);
            Assert.Equal(hookAddress, (string)webHookResultObject.url);
            Assert.True(((string)webHookResultObject.body).Contains(DeployStatus.Success.ToString()), "Missing Success from body");
            Assert.True(((string)webHookResultObject.body).Contains(commitId), "Missing commit id from body - " + commitId);
        }

        private async Task<string> GetWebHookResponseAsync(string hookAddress)
        {
            using (var httpClient = new HttpClient())
            {
                var response = await httpClient.GetAsync(hookAddress);
                string responseContent = await response.Content.ReadAsStringAsync();

                TestTracer.Trace("Received response: {0}", responseContent);

                return responseContent;
            }
        }
    }
}
