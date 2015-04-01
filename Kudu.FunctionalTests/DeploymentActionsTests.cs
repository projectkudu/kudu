using System.Linq;
using Kudu.Core.Deployment;
using Kudu.FunctionalTests.Infrastructure;
using Kudu.TestHarness;
using Kudu.TestHarness.Xunit;
using Xunit;

namespace Kudu.FunctionalTests
{
    [KuduXunitTestClass]
    public class DeploymentActionsTests
    {
        [Fact]
        public void PostDeploymentActionsShouldBeCalledOnSuccessfulDeployment()
        {
            string testName = "PostDeploymentActionsShouldBeCalledOnSuccessfulDeployment";
            string testLine1 = "test script 1 is running";
            string testLine2 = "test script 2 is running too";

            const string testCommitIdPrefix = @"SCM_COMMIT_ID = ";
            const string testCommitIdVariable = @"%SCM_COMMIT_ID%";

            using (new LatencyLogger(testName))
            {
                ApplicationManager.Run(testName, appManager =>
                {
                    using (var appRepository = Git.Clone("HelloKudu"))
                    {
                        TestTracer.Trace("Add action scripts");
                        appManager.VfsManager.WriteAllText(
                            @"site\deployments\tools\PostDeploymentActions\test_script_1.cmd",
                            @"@echo off
                              echo " + testLine1);

                        appManager.VfsManager.WriteAllText(
                            @"site\deployments\tools\PostDeploymentActions\test_script_2.bat",
                            @"@echo off
                              echo " + testLine2);

                        appManager.VfsManager.WriteAllText(
                            @"site\deployments\tools\PostDeploymentActions\test_script_3.sh",
                            @"@echo off
                              echo fail if we try to run this
                              exit /b 1");

                        appManager.VfsManager.WriteAllText(
                            @"site\deployments\tools\PostDeploymentActions\test_script_4.cmd",
                            @"@echo off
                              echo " + testCommitIdPrefix + testCommitIdVariable);

                        TestTracer.Trace("Deploy test app");
                        appManager.GitDeploy(appRepository.PhysicalPath);

                        TestTracer.Trace("Verify results");
                        var deploymentResults = appManager.DeploymentManager.GetResultsAsync().Result.ToList();
                        Assert.Equal(1, deploymentResults.Count);
                        Assert.Equal(DeployStatus.Success, deploymentResults[0].Status);

                        KuduAssert.VerifyLogOutput(appManager, deploymentResults[0].Id, testLine1, testLine2);
                        KuduAssert.VerifyLogOutput(appManager, deploymentResults[0].Id, testCommitIdPrefix + deploymentResults[0].Id);
                    }
                });
            }
        }

        [Fact]
        public void PostDeploymentActionWhichFailsShouldFailDeployment()
        {
            string testName = "PostDeploymentActionsShouldBeCalledOnSuccessfulPublish";
            string testLine1 = "test script 1 is running";
            string testLine2 = "test script 2 is running too";
            string failScriptCommand = "\n exit /b 1";

            using (new LatencyLogger(testName))
            {
                ApplicationManager.Run(testName, appManager =>
                {
                    using (var appRepository = Git.Clone("HelloKudu"))
                    {
                        TestTracer.Trace("Add action scripts, first script should fail, second script shouldn't run");
                        appManager.VfsManager.WriteAllText(
                            @"site\deployments\tools\PostDeploymentActions\test_script_1.cmd",
                            @"@echo off
                              echo " + testLine1 + failScriptCommand);

                        appManager.VfsManager.WriteAllText(
                            @"site\deployments\tools\PostDeploymentActions\test_script_2.bat",
                            @"@echo off
                              echo " + testLine2);

                        TestTracer.Trace("Deploy test app");
                        appManager.GitDeploy(appRepository.PhysicalPath);

                        TestTracer.Trace("Verify results");
                        var deploymentResults = appManager.DeploymentManager.GetResultsAsync().Result.ToList();
                        Assert.Equal(1, deploymentResults.Count);
                        Assert.Equal(DeployStatus.Failed, deploymentResults[0].Status);

                        KuduAssert.VerifyLogOutput(appManager, deploymentResults[0].Id, testLine1);
                        KuduAssert.VerifyLogOutputWithUnexpected(appManager, deploymentResults[0].Id, testLine2);

                        TestTracer.Trace("Update action scripts, first script should succeed, second script should fail");
                        appManager.VfsManager.WriteAllText(
                            @"site\deployments\tools\PostDeploymentActions\test_script_1.cmd",
                            @"@echo off
                              echo " + testLine1);

                        appManager.VfsManager.WriteAllText(
                            @"site\deployments\tools\PostDeploymentActions\test_script_2.bat",
                            @"@echo off
                              echo " + testLine2 + failScriptCommand);

                        TestTracer.Trace("Deploy test app");
                        appManager.DeploymentManager.DeployAsync(null).Wait();

                        TestTracer.Trace("Verify results");
                        deploymentResults = appManager.DeploymentManager.GetResultsAsync().Result.ToList();
                        Assert.Equal(1, deploymentResults.Count);
                        Assert.Equal(DeployStatus.Failed, deploymentResults[0].Status);

                        KuduAssert.VerifyLogOutput(appManager, deploymentResults[0].Id, testLine1, testLine2);
                    }
                });
            }
        }

        [Fact]
        public void PostDeploymentActionsShouldNotBeCalledOnFailedDeployment()
        {
            string testName = "PostDeploymentActionsShouldNotBeCalledOnFailedDeployment";
            string testLine1 = "test script 1 is running";
            string testLine2 = "test script 2 is running too";

            using (new LatencyLogger(testName))
            {
                ApplicationManager.Run(testName, appManager =>
                {
                    using (var appRepository = Git.Clone("WarningsAsErrors"))
                    {
                        TestTracer.Trace("Add action scripts");
                        appManager.VfsManager.WriteAllText(
                            @"site\deployments\tools\PostDeploymentActions\test_script_1.cmd",
                            @"@echo off
                              echo " + testLine1);

                        appManager.VfsManager.WriteAllText(
                            @"site\deployments\tools\PostDeploymentActions\test_script_2.bat",
                            @"@echo off
                              echo " + testLine2);

                        TestTracer.Trace("Deploy test app");
                        appManager.GitDeploy(appRepository.PhysicalPath);

                        TestTracer.Trace("Verify results");
                        var deploymentResults = appManager.DeploymentManager.GetResultsAsync().Result.ToList();
                        Assert.Equal(1, deploymentResults.Count);
                        Assert.Equal(DeployStatus.Failed, deploymentResults[0].Status);

                        KuduAssert.VerifyLogOutputWithUnexpected(appManager, deploymentResults[0].Id, testLine1, testLine2);
                    }
                });
            }
        }
    }
}