using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kudu.Client.Infrastructure;
using Kudu.Contracts.Jobs;
using Kudu.Contracts.Settings;
using Kudu.Core.Deployment;
using Kudu.TestHarness;
using Xunit;

namespace Kudu.FunctionalTests.Jobs
{
    public class WebJobsTests
    {
        private const string VerificationFilePath = "LogFiles/verification.txt";
        private const string JobsBinPath = "Site/wwwroot/App_Data/jobs";
        private const string JobsDataPath = "data/jobs";
        private const string ExpectedVerificationFileContent = "Verified!!!";
        private const string ExpectedChangedFileContent = "Changed!!!";
        private const string JobScript = "echo " + ExpectedVerificationFileContent + " >> %WEBROOT_PATH%\\..\\..\\LogFiles\\verification.txt\n";

        private const string ContinuousJobsBinPath = JobsBinPath + "/continuous";
        private const string BasicContinuousJobExecutablePath = ContinuousJobsBinPath + "/basicJob1/run.cmd";
        private const string ConsoleWorkerExecutablePath = ContinuousJobsBinPath + "/deployedJob/ConsoleWorker.exe";

        private const string TriggeredJobBinPath = "Site/wwwroot/App_Data/jobs/triggered";

        [Fact]
        public void PushAndRedeployContinuousJobAsConsoleWorker()
        {
            RunScenario("PushAndRedeployContinuousJobAsConsoleWorker", appManager =>
            {
                using (TestRepository testRepository = Git.Clone("ConsoleWorker"))
                {
                    ///////// Part 1
                    TestTracer.Trace("I) Starting ConsoleWorker test, deploying the worker");

                    PushAndVerifyConsoleWorker(appManager, testRepository, new string[] { ExpectedVerificationFileContent });

                    ///////// Part 2
                    TestTracer.Trace("II) Make sure redeploy works for console worker");
                    testRepository.Replace("ConsoleWorker\\Program.cs", ExpectedVerificationFileContent, ExpectedChangedFileContent);
                    Git.Commit(testRepository.PhysicalPath, "Made a small change");

                    PushAndVerifyConsoleWorker(appManager, testRepository, new string[] { ExpectedVerificationFileContent, ExpectedChangedFileContent }, expectedDeployments: 2);
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

                    PushAndVerifyConsoleWorker(appManager, testRepository, new string[] { ExpectedVerificationFileContent });

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

                WaitUntilAssertVerified(
                    "writing file",
                    TimeSpan.FromSeconds(10),
                    () => appManager.VfsManager.WriteAllText(BasicContinuousJobExecutablePath, JobScript));

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

                var expectedVerificationFileContents = new List<string>();
                expectedVerificationFileContents.Add(ExpectedVerificationFileContent);
                expectedVerificationFileContents.Add(ExpectedVerificationFileContent);

                WaitUntilAssertVerified(
                    "verification file",
                    TimeSpan.FromSeconds(30),
                    () => VerifyVerificationFile(appManager, expectedVerificationFileContents.ToArray()));

                appManager.JobsManager.SetSingletonContinuousJobAsync(expectedContinuousJob.Name, true).Wait();
                expectedVerificationFileContents.Add(ExpectedVerificationFileContent);
                expectedVerificationFileContents.Add(ExpectedVerificationFileContent);

                WaitUntilAssertVerified(
                    "verification file",
                    TimeSpan.FromSeconds(30),
                    () => VerifyVerificationFile(appManager, expectedVerificationFileContents.ToArray()));

                appManager.JobsManager.SetSingletonContinuousJobAsync(expectedContinuousJob.Name, false).Wait();
                expectedVerificationFileContents.Add(ExpectedVerificationFileContent);
                expectedVerificationFileContents.Add(ExpectedVerificationFileContent);

                WaitUntilAssertVerified(
                    "verification file",
                    TimeSpan.FromSeconds(30),
                    () => VerifyVerificationFile(appManager, expectedVerificationFileContents.ToArray()));
            });
        }

        [Fact]
        public void ContinuousJobStopsWhenDisabledStartsWhenEnabled()
        {
            RunScenario("ContinuousJobStopsWhenDisabledStartsWhenEnabled", appManager =>
            {
                using (TestRepository testRepository = Git.Clone("ConsoleWorker"))
                {
                    TestTracer.Trace("Starting ConsoleWorker test, deploying the worker");

                    PushAndVerifyConsoleWorker(appManager, testRepository, new string[] { ExpectedVerificationFileContent });

                    TestTracer.Trace("Make sure process is up");
                    var processes = appManager.ProcessManager.GetProcessesAsync().Result;
                    var workerProcess = processes.FirstOrDefault(p => String.Equals("ConsoleWorker", p.Name, StringComparison.OrdinalIgnoreCase));
                    Assert.NotNull(workerProcess);

                    appManager.JobsManager.DisableContinuousJobAsync("deployedJob").Wait();

                    WaitUntilAssertVerified(
                        "continuous job disabled",
                        TimeSpan.FromSeconds(30),
                        () =>
                        {
                            var jobs = appManager.JobsManager.ListContinuousJobsAsync().Result;
                            Assert.Equal(1, jobs.Count());
                            Assert.Equal("Stopped", jobs.First().Status);
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

                    appManager.JobsManager.EnableContinuousJobAsync("deployedJob").Wait();

                    WaitUntilAssertVerified(
                        "continuous job enabled",
                        TimeSpan.FromSeconds(30),
                        () =>
                        {
                            var jobs = appManager.JobsManager.ListContinuousJobsAsync().Result;
                            Assert.Equal(1, jobs.Count());
                            Assert.Equal("Running", jobs.First().Status);
                        });

                    WaitUntilAssertVerified(
                        "make sure process is up",
                        TimeSpan.FromSeconds(30),
                        () =>
                        {
                            var allProcesses = appManager.ProcessManager.GetProcessesAsync().Result;
                            var process = allProcesses.FirstOrDefault(p => String.Equals("ConsoleWorker", p.Name, StringComparison.OrdinalIgnoreCase));
                            Assert.NotNull(process);
                        });
                }
            });
        }

        [Fact]
        public void TriggeredJobTriggers()
        {
            RunScenario("TriggeredJobTriggers", appManager =>
            {
                const string jobName = "job1";

                TestTracer.Trace("Copying the script to the triggered job directory");

                WaitUntilAssertVerified(
                    "writing file",
                    TimeSpan.FromSeconds(10),
                    () => appManager.VfsManager.WriteAllText(TriggeredJobBinPath + "/" + jobName + "/run.cmd", JobScript));

                var expectedTriggeredJob = new TriggeredJob()
                {
                    Name = jobName,
                    JobType = "triggered",
                    RunCommand = "run.cmd"
                };

                TestTracer.Trace("Verify triggered job exists");

                TriggeredJob triggeredJob = appManager.JobsManager.GetTriggeredJobAsync(jobName).Result;
                AssertTriggeredJob(expectedTriggeredJob, triggeredJob);

                TestTracer.Trace("Trigger the job");

                VerifyTriggeredJobTriggers(appManager, jobName, 1, "Success", "echo ");

                VerifyVerificationFile(appManager, new string[] { ExpectedVerificationFileContent });

                TestTracer.Trace("Trigger the job again");

                VerifyTriggeredJobTriggers(appManager, jobName, 2, "Success", "echo ");

                VerifyVerificationFile(appManager, new string[] { ExpectedVerificationFileContent, ExpectedVerificationFileContent });

                TestTracer.Trace("Trigger the job 5 more times to make sure history is trimmed");

                appManager.SettingsManager.SetValue(SettingsKeys.MaxJobRunsHistoryCount, "5").Wait();

                VerifyTriggeredJobTriggers(appManager, jobName, 3, "Success", "echo ");
                VerifyTriggeredJobTriggers(appManager, jobName, 4, "Success", "echo ");
                VerifyTriggeredJobTriggers(appManager, jobName, 5, "Success", "echo ");
                VerifyTriggeredJobTriggers(appManager, jobName, 5, "Success", "echo ");
                VerifyTriggeredJobTriggers(appManager, jobName, 5, "Success", "echo ");
            });
        }

        [Fact]
        public void ExtraInfoUrlTemplateShouldBeUsedWhenExists()
        {
            RunScenario("ExtraInfoUrlTemplateShouldBeUsedWhenExists", appManager =>
            {
                TestTracer.Trace("Adding jobs");

                var extraInfoUrlTemplates = new string[]
                {
                    "/sb?jobName={jobName}&jobType={jobType}",
                    "",
                    null,
                    "/some/other/link",
                    "/some/other/{jobName}/link",
                    "http://someplace.else.com/{jobType}/index.html"
                };

                // Extract expected url (remove any user/password that might exists in the service url)
                var serviceUrl = new Uri(appManager.ServiceUrl);
                string expectedBaseUrl = appManager.ServiceUrl.Replace(serviceUrl.UserInfo + '@', String.Empty);

                var extraInfoUrlExpectedResults = new string[]
                {
                    expectedBaseUrl + "sb?jobName=job1&jobType=triggered",
                    expectedBaseUrl + "JobRuns/history.html?jobName=job2",
                    expectedBaseUrl + "JobRuns/history.html?jobName=job3",
                    expectedBaseUrl + "some/other/link",
                    expectedBaseUrl + "some/other/job5/link",
                    "http://someplace.else.com/triggered/index.html"
                };

                int index = 1;
                foreach (string extraInfoUrlTemplate in extraInfoUrlTemplates)
                {
                    string jobName = "job" + index;
                    string jobScriptPath = TriggeredJobBinPath + "/" + jobName + "/run.cmd";
                    string jobSettingsPath = TriggeredJobBinPath + "/" + jobName + "/job.settings.json";

                    appManager.VfsManager.WriteAllText(jobScriptPath, "echo echo echo echo");

                    if (extraInfoUrlTemplate != null)
                    {
                        string jobSettingsContent = "{\"extra_info_url_template\":\"" + extraInfoUrlTemplate + "\"}";
                        appManager.VfsManager.WriteAllText(jobSettingsPath, jobSettingsContent);
                    }

                    index++;
                }

                index = 1;
                foreach (string extraUrlExpectedResult in extraInfoUrlExpectedResults)
                {
                    TestTracer.Trace("Verify - " + extraUrlExpectedResult);

                    string jobName = "job" + index;

                    TriggeredJob triggeredJob = appManager.JobsManager.GetTriggeredJobAsync(jobName).Result;
                    Assert.Equal(extraUrlExpectedResult, triggeredJob.ExtraInfoUrl.ToString());

                    index++;
                }
            });
        }

        private void VerifyTriggeredJobTriggers(ApplicationManager appManager, string jobName, int expectedNumberOfRuns, string expectedStatus, string expectedOutput = null, string expectedError = null)
        {
            appManager.JobsManager.InvokeTriggeredJobAsync(jobName).Wait();

            WaitUntilAssertVerified(
                "verify triggered job run",
                TimeSpan.FromSeconds(20),
                () =>
                {
                    TriggeredJobHistory triggeredJobHistory = appManager.JobsManager.GetTriggeredJobHistoryAsync(jobName).Result;
                    Assert.NotNull(triggeredJobHistory);
                    Assert.Equal(expectedNumberOfRuns, triggeredJobHistory.TriggeredJobRuns.Count());

                    foreach (TriggeredJobRun triggeredJobRun in triggeredJobHistory.TriggeredJobRuns)
                    {
                        AssertTriggeredJobRun(appManager, triggeredJobRun, expectedStatus, expectedOutput, expectedError);
                    }
                });
        }

        [Fact]
        public void JobsScriptsShouldBeChosenByOrder()
        {
            RunScenario("JobsScriptsShouldBeChosenByOrder", appManager =>
            {
                const string jobName = "job1";

                TestTracer.Trace("Adding all scripts");

                var scriptFileNames = new string[]
                {
                    "run.cmd",
                    "run.bat",
                    "run.exe",
                    "run.sh",
                    //"run.py",
                    //"run.php",
                    "run.js",
                    "go.cmd",
                    "do.bat",
                    "console.exe",
                    "invoke.sh",
                    //"respond.py",
                    //"request.php",
                    "execute.js"
                };

                foreach (string scriptFileName in scriptFileNames)
                {
                    appManager.VfsManager.WriteAllText(TriggeredJobBinPath + "/" + jobName + "/" + scriptFileName, JobScript);
                }

                foreach (string scriptFileName in scriptFileNames)
                {
                    TestTracer.Trace("Verify - " + scriptFileName);

                    var expectedTriggeredJob = new TriggeredJob()
                    {
                        Name = jobName,
                        JobType = "triggered",
                        RunCommand = scriptFileName
                    };

                    TriggeredJob triggeredJob = appManager.JobsManager.GetTriggeredJobAsync(jobName).Result;
                    AssertTriggeredJob(expectedTriggeredJob, triggeredJob);

                    appManager.VfsManager.Delete(TriggeredJobBinPath + "/" + jobName + "/" + scriptFileName);
                }
            });
        }

        private void VerifyVerificationFile(ApplicationManager appManager, string[] expectedContentLines)
        {
            string verificationFileContent = appManager.VfsManager.ReadAllText(VerificationFilePath).TrimEnd();
            Assert.NotNull(verificationFileContent);
            string[] lines = verificationFileContent.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(expectedContentLines.Length, lines.Length);
            for (int i = 0; i < expectedContentLines.Length; i++)
            {
                Assert.Equal(expectedContentLines[i], lines[i].Trim());
            }
        }

        private void AssertTriggeredJobRun(ApplicationManager appManager, TriggeredJobRun actualTriggeredJobRun, string expectedStatus, string expectedOutput = null, string expectedError = null)
        {
            Assert.NotNull(actualTriggeredJobRun);
            Assert.Equal(expectedStatus, actualTriggeredJobRun.Status);
            Assert.NotNull(actualTriggeredJobRun.Duration);
            Assert.NotNull(actualTriggeredJobRun.EndTime);
            Assert.NotNull(actualTriggeredJobRun.Id);
            Assert.NotNull(actualTriggeredJobRun.StartTime);
            Assert.NotNull(actualTriggeredJobRun.Url);

            AssertUrlContentAsync(appManager, actualTriggeredJobRun.OutputUrl, expectedOutput).Wait();
            AssertUrlContentAsync(appManager, actualTriggeredJobRun.ErrorUrl, expectedError).Wait();
        }

        private async Task AssertUrlContentAsync(ApplicationManager appManager, Uri requestUrl, string expectedContent)
        {
            if (expectedContent == null)
            {
                Assert.Null(requestUrl);
                return;
            }

            string address = requestUrl.ToString();
            using (var httpClient = HttpClientHelper.CreateClient(address, appManager.JobsManager.Credentials))
            {
                using (var response = await httpClient.GetAsync(String.Empty))
                {
                    var content = await response.Content.ReadAsStringAsync();
                    TestTracer.Trace("Request to: {0}\nStatus code: {1}\nContent: {2}", address, response.StatusCode, content);

                    Assert.True(content.Contains(expectedContent), "Expected content: " + expectedContent);
                }
            }
        }

        private void RunScenario(string name, Action<ApplicationManager> action)
        {
            ApplicationManager.Run(name, appManager =>
            {
                appManager.SettingsManager.SetValue(SettingsKeys.JobsInterval, "5").Wait();

                CleanupTest(appManager);

                try
                {
                    action(appManager);
                }
                finally
                {
                    try
                    {
                        CleanupTest(appManager);
                    }
                    catch (Exception ex)
                    {
                        TestTracer.Trace(ex.ToString());
                    }
                }
            });
        }

        private void CleanupTest(ApplicationManager appManager)
        {
            WaitUntilAssertVerified(
                "clean site for jobs",
                TimeSpan.FromSeconds(60),
                () =>
                {
                    appManager.VfsManager.Delete(JobsBinPath + "?recursive=true");
                    appManager.VfsManager.Delete(JobsDataPath + "?recursive=true");
                    appManager.VfsManager.Delete(VerificationFilePath);
                });
        }

        private void PushAndVerifyConsoleWorker(ApplicationManager appManager, TestRepository testRepository, string[] expectedVerificationFileLines, int expectedDeployments = 1)
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
                    VerifyVerificationFile(appManager, expectedVerificationFileLines);
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

        private void AssertJob(JobBase expectedJob, JobBase actualJob)
        {
            Assert.NotNull(actualJob);
            Assert.Equal(expectedJob.Name, actualJob.Name);
            Assert.Equal(expectedJob.RunCommand, actualJob.RunCommand);
            Assert.Equal(expectedJob.JobType, actualJob.JobType);
            Assert.NotNull(actualJob.Url);
            Assert.NotNull(actualJob.ExtraInfoUrl);
        }

        private void AssertContinuousJob(ContinuousJob expectedContinuousJob, ContinuousJob actualContinuousJob)
        {
            AssertJob(expectedContinuousJob, actualContinuousJob);
            Assert.NotNull(actualContinuousJob);
            Assert.Equal(expectedContinuousJob.Status, actualContinuousJob.Status);
            Assert.NotNull(actualContinuousJob.LogUrl);
        }

        private void AssertTriggeredJob(TriggeredJob expectedTriggeredJob, TriggeredJob actualTriggeredJob)
        {
            AssertJob(expectedTriggeredJob, actualTriggeredJob);
            Assert.NotNull(actualTriggeredJob);
            Assert.NotNull(actualTriggeredJob.HistoryUrl);
        }
    }
}