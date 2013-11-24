using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Kudu.Contracts.Jobs;
using Kudu.Contracts.Settings;
using Kudu.Core.Deployment;
using Kudu.TestHarness;
using Xunit;

namespace Kudu.FunctionalTests.Jobs
{
    public class ContinuousJobsTests
    {
        private const string VerificationFilePath = "LogFiles/verification.txt";
        private const string ContinuousJobBinPath = "Site/wwwroot/App_Data/jobs/continuous";
        private const string BasicContinuousJobExecutablePath = ContinuousJobBinPath + "/basicJob1/run.cmd";
        private const string ConsoleWorkerExecutablePath = ContinuousJobBinPath + "/deployedJob/ConsoleWorker.exe";
        private const string ExpectedVerificationFileContent = "Verified!!!";
        private const string ExpectedChangedFileContent = "Changed!!!";

        [Fact]
        public void PushAndRedeployContinuousJobAsConsoleWorker()
        {
            RunScenario("PushAndRedeployContinuousJobAsConsoleWorker", appManager =>
            {
                using (TestRepository testRepository = Git.Clone("ConsoleWorker"))
                {
                    ///////// Part 1
                    TestTracer.Trace("I) Starting ConsoleWorker test, deploying the worker");

                    PushAndVerifyConsoleWorker(appManager, testRepository, ExpectedVerificationFileContent);

                    ///////// Part 2
                    TestTracer.Trace("II) Make sure redeploy works for console worker");
                    testRepository.Replace("ConsoleWorker\\Program.cs", ExpectedVerificationFileContent, ExpectedChangedFileContent);
                    Git.Commit(testRepository.PhysicalPath, "Made a small change");

                    PushAndVerifyConsoleWorker(appManager, testRepository, ExpectedVerificationFileContent + System.Environment.NewLine + ExpectedChangedFileContent, expectedDeployments: 2);
                }
            });
        }

        [Fact]
        public void DeleteConsoleWorkerExecutableStopsIt()
        {
            RunScenario("DeleteConsoleWorkerExecutableStopsIt", appManager =>
            {
                using (TestRepository testRepository = Git.Clone("ConsoleWorker"))
                {
                    ///////// Part 1
                    TestTracer.Trace("I) Starting ConsoleWorker test, deploying the worker");

                    PushAndVerifyConsoleWorker(appManager, testRepository, ExpectedVerificationFileContent);

                    TestTracer.Trace("Make sure process is up");
                    var processes = appManager.ProcessManager.GetProcessesAsync().Result;
                    var workerProcess = processes.FirstOrDefault(p => String.Equals("ConsoleWorker", p.Name, StringComparison.OrdinalIgnoreCase));
                    Assert.NotNull(workerProcess);

                    ///////// Part 2
                    TestTracer.Trace("II) Verifying worker gone when executable file is removed");

                    appManager.VfsManager.Delete(ConsoleWorkerExecutablePath);

                    WaitUntilAssertVerified(
                        "no continuous jobs exist",
                        TimeSpan.FromSeconds(60),
                        () =>
                        {
                            var jobs = appManager.JobsManager.ListContinuousJobsAsync().Result;
                            Assert.Equal(0, jobs.Count());
                        });

                    WaitUntilAssertVerified(
                        "make sure process is down",
                        TimeSpan.FromSeconds(30),
                        () =>
                        {
                            var allProcesses = appManager.ProcessManager.GetProcessesAsync().Result;
                            var process = allProcesses.FirstOrDefault(p => String.Equals("ConsoleWorker", p.Name, StringComparison.OrdinalIgnoreCase));
                            Assert.Null(process);
                        });
                }
            });
        }

        [Fact]
        public void ContinuousJobStartsAfterGoingDown()
        {
            RunScenario("ContinuousJobStartsAfterGoingDown", appManager =>
            {
                TestTracer.Trace("Copying the script to the continuous job directory");

                const string basicContinuousJobScript = "echo " + ExpectedVerificationFileContent + " >> %WEBROOT_PATH%\\..\\..\\LogFiles\\verification.txt\n";

                WaitUntilAssertVerified(
                    "writing file",
                    TimeSpan.FromSeconds(10),
                    () => appManager.VfsManager.WriteAllText(BasicContinuousJobExecutablePath, basicContinuousJobScript));

                var expectedContinuousJob = new ContinuousJob()
                {
                    Name = "basicJob1",
                    JobType = "continuous",
                    Status = "PendingRestart",
                    RunCommand = "run.cmd"
                };

                WaitUntilAssertVerified(
                    "verify continuous job",
                    TimeSpan.FromSeconds(60),
                    () =>
                    {
                        ContinuousJob deployedJob = appManager.JobsManager.GetContinuousJobAsync("basicJob1").Result;
                        AssertContinuousJob(expectedContinuousJob, deployedJob);
                    });

                TestTracer.Trace("Waiting for verification file to have 2 lines (which means it ran twice)");

                WaitUntilAssertVerified(
                    "verification file",
                    TimeSpan.FromSeconds(30),
                    () =>
                    {
                        string verificationFileContent = appManager.VfsManager.ReadAllText(VerificationFilePath).TrimEnd();
                        string[] lines = verificationFileContent.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                        Assert.True(lines.Length > 1, "Missing verification lines");
                        Assert.Equal(lines[0].Trim(), ExpectedVerificationFileContent);
                        Assert.Equal(lines[1].Trim(), ExpectedVerificationFileContent);
                    });
            });
        }

        private void RunScenario(string name, Action<ApplicationManager> action)
        {
            ApplicationManager.Run(name, appManager =>
            {
                appManager.SettingsManager.SetValue(SettingsKeys.JobsInterval, "5").Wait();

                // Make sure verification file and jobs doesn't exist
                WaitUntilAssertVerified(
                    "clean site for jobs",
                    TimeSpan.FromSeconds(60),
                    () =>
                    {
                        appManager.VfsManager.Delete(VerificationFilePath);
                        appManager.VfsManager.Delete(ContinuousJobBinPath + "?recursive=true");
                    });

                action(appManager);
            });
        }

        private void PushAndVerifyConsoleWorker(ApplicationManager appManager, TestRepository testRepository, string expectedVerificationFileContent, int expectedDeployments = 1)
        {
            appManager.GitDeploy(testRepository.PhysicalPath);
            var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

            Assert.Equal(expectedDeployments, results.Count);
            for (int i = 0; i < expectedDeployments; i++)
            {
                Assert.Equal(DeployStatus.Success, results[i].Status);
            }

            var expectedContinuousJob = new ContinuousJob()
            {
                Name = "deployedJob",
                JobType = "continuous",
                Status = "Running",
                RunCommand = "ConsoleWorker.exe"
            };

            WaitUntilAssertVerified(
                "verify continuous job",
                TimeSpan.FromSeconds(60),
                () =>
                {
                    ContinuousJob deployedJob = appManager.JobsManager.GetContinuousJobAsync("deployedJob").Result;
                    AssertContinuousJob(expectedContinuousJob, deployedJob);
                });

            WaitUntilAssertVerified(
                "verification file",
                TimeSpan.FromSeconds(30),
                () =>
                {
                    string verificationFileContent = appManager.VfsManager.ReadAllText(VerificationFilePath).TrimEnd();
                    Assert.Equal(expectedVerificationFileContent, verificationFileContent);
                });
        }

        private void WaitUntilAssertVerified(string description, TimeSpan maxWaitTime, Action assertAction)
        {
            TestTracer.Trace("Waiting for " + description);

            Stopwatch waitTime = Stopwatch.StartNew();

            while (waitTime.Elapsed < maxWaitTime)
            {
                try
                {
                    assertAction();
                    return;
                }
                catch
                {
                }

                Thread.Sleep(1000);
            }

            assertAction();
        }

        private void AssertContinuousJob(ContinuousJob expectedContinuousJob, ContinuousJob deployedJob)
        {
            Assert.NotNull(deployedJob);
            Assert.Equal(expectedContinuousJob.Name, deployedJob.Name);
            Assert.Equal(expectedContinuousJob.RunCommand, deployedJob.RunCommand);
            Assert.Equal(expectedContinuousJob.JobType, deployedJob.JobType);
            Assert.Equal(expectedContinuousJob.Status, deployedJob.Status);
            Assert.NotNull(deployedJob.Url);
            Assert.NotNull(deployedJob.LogUrl);
            Assert.NotNull(deployedJob.ExtraInfoUrl);
        }
    }
}