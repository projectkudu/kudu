using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Kudu.Client;
using Kudu.Client.Infrastructure;
using Kudu.Contracts.Jobs;
using Kudu.Contracts.Settings;
using Kudu.Core.Deployment;
using Kudu.Core.Hooks;
using Kudu.TestHarness;
using Kudu.TestHarness.Xunit;
using Newtonsoft.Json;
using Xunit;

namespace Kudu.FunctionalTests.Jobs
{
    [KuduXunitTestClass]
    public class WebJobsTests
    {
        private const string JobsBinPath = "Site/wwwroot/App_Data/jobs";
        private const string JobsDataPath = "data/jobs";
        private const string ExpectedVerificationFileContent = "Verified!!!";
        private const string ExpectedChangedFileContent = "Changed!!!";
        private const string JobScript = "echo " + ExpectedVerificationFileContent + " >> %WEBROOT_PATH%\\..\\..\\LogFiles\\verification.txt.%WEBSITE_INSTANCE_ID%\n";

        private const string ContinuousJobsBinPath = JobsBinPath + "/continuous";
        private const string ConsoleWorkerJobPath = ContinuousJobsBinPath + "/deployedJob";
        private const string ConsoleWorkerExecutablePath = ConsoleWorkerJobPath + "/ConsoleWorker.exe";

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

#if NOTYET
                    WaitUntilAssertVerified(
                        "runnable script is missing",
                        TimeSpan.FromSeconds(60),
                        () =>
                        {
                            var job = appManager.JobsManager.GetContinuousJobAsync("deployedJob").Result;
                            Assert.Null(job.RunCommand);
                            Assert.Equal("No runnable script file was found.", job.Error);
                        });

                    appManager.VfsManager.Delete(ConsoleWorkerJobPath, recursive: true);
#endif

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

                appManager.JobsManager.CreateContinuousJobAsync("basicJob1", "run.cmd", JobScript).Wait();

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

                TestTracer.Trace("Verify continuous job settings and set it to isSingleton: true");
                JobSettings continuousJobSettings =
                    appManager.JobsManager.GetContinuousJobSettingsAsync(expectedContinuousJob.Name).Result;

                Assert.False(continuousJobSettings.IsSingleton);

                continuousJobSettings.SetSetting("is_singleton", true);
                appManager.JobsManager.SetContinuousJobSettingsAsync(expectedContinuousJob.Name, continuousJobSettings).Wait();

                expectedVerificationFileContents.Add(ExpectedVerificationFileContent);
                expectedVerificationFileContents.Add(ExpectedVerificationFileContent);

                WaitUntilAssertVerified(
                    "verification file",
                    TimeSpan.FromSeconds(30),
                    () => VerifyVerificationFile(appManager, expectedVerificationFileContents.ToArray()));

                TestTracer.Trace("Verify continuous job settings and set it to isSingleton: false");
                continuousJobSettings =
                    appManager.JobsManager.GetContinuousJobSettingsAsync(expectedContinuousJob.Name).Result;

                Assert.True(continuousJobSettings.GetSetting<bool>("is_singleton"));

                continuousJobSettings.SetSetting("is_singleton", false);
                appManager.JobsManager.SetContinuousJobSettingsAsync(expectedContinuousJob.Name, continuousJobSettings).Wait();

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

                    TestTracer.Trace("Disable this job");
                    appManager.JobsManager.DisableContinuousJobAsync("deployedJob").Wait();

                    VerifyContinuousJobDisabled(appManager);

                    TestTracer.Trace("Enable this job");
                    appManager.JobsManager.EnableContinuousJobAsync("deployedJob").Wait();

                    VerifyContinuousJobEnabled(appManager);

                    TestTracer.Trace("Disable all WebJobs");
                    appManager.SettingsManager.SetValue(SettingsKeys.WebJobsStopped, "1").Wait();
                    RestartServiceSite(appManager);

                    VerifyContinuousJobDisabled(appManager);

                    TestTracer.Trace("Enable all WebJobs");
                    appManager.SettingsManager.SetValue(SettingsKeys.WebJobsStopped, "0").Wait();
                    RestartServiceSite(appManager);

                    VerifyContinuousJobEnabled(appManager);
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

                appManager.JobsManager.CreateTriggeredJobAsync(jobName, "run.cmd", JobScript).Wait();

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

                TestTracer.Trace("Disable all WebJobs");
                appManager.SettingsManager.SetValue(SettingsKeys.WebJobsStopped, "1").Wait();
                VerifyTriggeredJobDoesNotTrigger(appManager, jobName).Wait();

                TestTracer.Trace("Enable all WebJobs");
                appManager.SettingsManager.SetValue(SettingsKeys.WebJobsStopped, "0").Wait();
                VerifyTriggeredJobTriggers(appManager, jobName, 3, "Success", "echo ");

                appManager.SettingsManager.SetValue(SettingsKeys.WebJobsHistorySize, "5").Wait();

                VerifyTriggeredJobTriggers(appManager, jobName, 4, "Success", "echo ");
                VerifyTriggeredJobTriggers(appManager, jobName, 5, "Success", "echo ");
                VerifyTriggeredJobTriggers(appManager, jobName, 5, "Success", "echo ");
                VerifyTriggeredJobTriggers(appManager, jobName, 5, "Success", "echo ");
            });
        }

        [Fact]
        public void TriggeredJobAcceptsArguments()
        {
            RunScenario("TriggeredJobAcceptsArguments", appManager =>
            {
                const string jobName = "job1";

                TestTracer.Trace("Copying the script to the triggered job directory");

                appManager.JobsManager.CreateTriggeredJobAsync(jobName, "run.cmd", "echo %*").Wait();

                var expectedTriggeredJob = new TriggeredJob()
                {
                    Name = jobName,
                    JobType = "triggered",
                    RunCommand = "run.cmd"
                };

                TestTracer.Trace("Trigger the job");

                VerifyTriggeredJobTriggers(appManager, jobName, 1, "Success", "echo test arguments", expectedError: null, arguments: "test arguments");
            });
        }

        [Fact]
        public void TriggeredJobCallsSubscribedWebHooks()
        {
            const string hook = "hookCalled/1";
            const string jobName = "myjob";

            using (new LatencyLogger("TriggeredJobCallsSubscribedWebHooks"))
            {
                RunScenario("TriggeredJobTriggers", appManager =>
                {
                    using (var hookAppRepository = Git.Clone("NodeWebHookTest"))
                    {
                        appManager.GitDeploy(hookAppRepository.PhysicalPath);

                        string hookAddress = appManager.SiteUrl + hook;

                        TestTracer.Trace("Subscribe web hook to " + hookAddress);
                        appManager.WebHooksManager.SubscribeAsync(new WebHook(HookEventTypes.TriggeredJobFinished, hookAddress)).Wait();

                        appManager.JobsManager.CreateTriggeredJobAsync(jobName, "run.cmd", JobScript).Wait();
                        appManager.JobsManager.InvokeTriggeredJobAsync(jobName).Wait();

                        WaitUntilAssertVerified(
                            "verify webhook called",
                            TimeSpan.FromSeconds(10),
                            () =>
                            {
                                TestTracer.Trace("Verify webhook called");
                                using (var httpClient = new HttpClient())
                                {
                                    var response = httpClient.GetAsync(hookAddress).Result;
                                    string responseContent = response.Content.ReadAsStringAsync().Result;

                                    TestTracer.Trace("Received response: {0}", responseContent);

                                    string[] webHookResults = responseContent.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

                                    Assert.Equal(1, webHookResults.Count());

                                    dynamic webHookResultJson = JsonConvert.DeserializeObject(webHookResults[0]);
                                    var triggeredJobRun = JsonConvert.DeserializeObject<TriggeredJobRun>((string)webHookResultJson.body);
                                    AssertTriggeredJobRun(appManager, triggeredJobRun, jobName, "Success", "echo ");
                                }
                            });
                    }
                });
            }
        }

        [Fact]
        public void JobsSettingsSetSuccessfully()
        {
            RunScenario("JobsSettingsSetSuccessfully", appManager =>
            {
                const string triggeredJobName = "triggeredJob";
                const string continuousJobName = "continuousJob";

                const string settingKey = "mysetting";
                const string settingValue = "myvalue";

                TestTracer.Trace("Creating a triggered job and creating a continuous job");

                appManager.JobsManager.CreateTriggeredJobAsync(triggeredJobName, "run.cmd", JobScript).Wait();
                appManager.JobsManager.CreateContinuousJobAsync(continuousJobName, "run.cmd", JobScript).Wait();

                TestTracer.Trace("Test update of continuous job settings");

                JobSettings continuousJobSettings = appManager.JobsManager.GetContinuousJobSettingsAsync(continuousJobName).Result;
                Assert.Equal(null, continuousJobSettings.GetSetting<string>(settingKey));

                continuousJobSettings.SetSetting(settingKey, settingValue);
                appManager.JobsManager.SetContinuousJobSettingsAsync(continuousJobName, continuousJobSettings).Wait();

                continuousJobSettings = appManager.JobsManager.GetContinuousJobSettingsAsync(continuousJobName).Result;
                Assert.Equal(settingValue, continuousJobSettings.GetSetting<string>(settingKey));

                TestTracer.Trace("Test update of triggered job settings");

                JobSettings triggeredJobSettings = appManager.JobsManager.GetTriggeredJobSettingsAsync(triggeredJobName).Result;
                Assert.Equal(null, triggeredJobSettings.GetSetting<string>(settingKey));

                triggeredJobSettings.SetSetting(settingKey, settingValue);
                appManager.JobsManager.SetTriggeredJobSettingsAsync(triggeredJobName, triggeredJobSettings).Wait();

                triggeredJobSettings = appManager.JobsManager.GetTriggeredJobSettingsAsync(triggeredJobName).Result;
                Assert.Equal(settingValue, triggeredJobSettings.GetSetting<string>(settingKey));
            });
        }

        [Fact]
        public void InPlaceJobRunsInPlace()
        {
            RunScenario("InPlaceJobRunsInPlace", appManager =>
            {
                const string jobName = "joba";
                const string expectedTempStr = "Temp\\jobs\\triggered\\" + jobName;
                const string expectedAppDataStr = "App_Data\\jobs\\triggered\\" + jobName;

                TestTracer.Trace("Create the triggered WebJob");
                appManager.JobsManager.CreateTriggeredJobAsync(jobName, "run.cmd", "cd\n").Wait();

                TestTracer.Trace("Trigger the triggered WebJob");
                VerifyTriggeredJobTriggers(appManager, jobName, 1, "Success", expectedTempStr);

                var jobSettings = new JobSettings();

                TestTracer.Trace("Set triggered WebJob to is_in_place true");
                jobSettings.SetSetting("is_in_place", true);
                appManager.JobsManager.SetTriggeredJobSettingsAsync(jobName, jobSettings).Wait();

                TestTracer.Trace("Trigger the triggered WebJob");
                VerifyTriggeredJobTriggers(appManager, jobName, 2, "Success", expectedAppDataStr);

                TestTracer.Trace("Set triggered WebJob to is_in_place false");
                jobSettings.SetSetting("is_in_place", false);
                appManager.JobsManager.SetTriggeredJobSettingsAsync(jobName, jobSettings).Wait();

                TestTracer.Trace("Trigger the triggered WebJob");
                VerifyTriggeredJobTriggers(appManager, jobName, 3, "Success", expectedTempStr);

                TestTracer.Trace("Create the continuous WebJob");
                appManager.JobsManager.CreateContinuousJobAsync(jobName, "run.cmd", "cd > %WEBROOT_PATH%\\..\\..\\LogFiles\\verification.txt.%WEBSITE_INSTANCE_ID%\n").Wait();

                WaitUntilAssertVerified(
                    "verification file",
                    TimeSpan.FromSeconds(30),
                    () => VerifyVerificationFile(appManager, new[] { expectedTempStr }));

                TestTracer.Trace("Set continuous WebJob to is_in_place true");
                jobSettings.SetSetting("is_in_place", true);
                appManager.JobsManager.SetContinuousJobSettingsAsync(jobName, jobSettings).Wait();

                WaitUntilAssertVerified(
                    "verification file",
                    TimeSpan.FromSeconds(30),
                    () => VerifyVerificationFile(appManager, new[] { expectedAppDataStr }));

                TestTracer.Trace("Set continuous WebJob to is_in_place false");
                jobSettings.SetSetting("is_in_place", false);

                appManager.JobsManager.SetContinuousJobSettingsAsync(jobName, jobSettings).Wait();
                WaitUntilAssertVerified(
                    "verification file",
                    TimeSpan.FromSeconds(30),
                    () => VerifyVerificationFile(appManager, new[] { expectedTempStr }));
            });
        }

        [Fact]
        public void CreateSameTriggeredJobTwiceUpdatesJobBinaries()
        {
            RunScenario("CreateSameTriggeredJobTwiceUpdatesJobBinaries", appManager =>
            {
                const string secondScriptName = "myrun.cmd";
                const string jobName = "job1";
                const string jobPath = JobsBinPath + "/triggered/" + jobName + "/";

                string zippedJobBinaries = BuildZippedJobBinaries();

                appManager.JobsManager.CreateTriggeredJobAsync(jobName, zippedJobBinaries).Wait();
                Assert.True(appManager.VfsManager.Exists(jobPath + "inner/run.cmd"));
                Assert.False(appManager.VfsManager.Exists(jobPath + secondScriptName));

                TestTracer.Trace("Second triggered job creation should replace current binaries");
                appManager.JobsManager.CreateTriggeredJobAsync(jobName, secondScriptName, "echo test test test test").Wait();
                Assert.False(appManager.VfsManager.Exists(jobPath + "inner/run.cmd"));
                Assert.True(appManager.VfsManager.Exists(jobPath + secondScriptName));
            });
        }

        [Fact]
        public void CreateSameContinuousJobTwiceUpdatesJobBinaries()
        {
            RunScenario("CreateSameContinuousJobTwiceUpdatesJobBinaries", appManager =>
            {
                const string secondScriptName = "myrun.cmd";
                const string jobName = "job1";
                const string jobPath = JobsBinPath + "/continuous/" + jobName + "/";

                string zippedJobBinaries = BuildZippedJobBinaries();

                appManager.JobsManager.CreateContinuousJobAsync(jobName, zippedJobBinaries).Wait();
                Assert.True(appManager.VfsManager.Exists(jobPath + "inner/run.cmd"));
                Assert.False(appManager.VfsManager.Exists(jobPath + secondScriptName));

                TestTracer.Trace("Second continuous job creation should replace current binaries");
                appManager.JobsManager.CreateContinuousJobAsync(jobName, secondScriptName, "echo test test test test").Wait();
                Assert.False(appManager.VfsManager.Exists(jobPath + "inner/run.cmd"));
                Assert.True(appManager.VfsManager.Exists(jobPath + secondScriptName));
            });
        }

        [Fact]
        public void CreateAndDeleteJobSucceeds()
        {
            RunScenario("CreateAndDeleteJobSucceeds", appManager =>
            {
                string zippedJobBinaries = BuildZippedJobBinaries();

                appManager.JobsManager.CreateTriggeredJobAsync("job1", zippedJobBinaries).Wait();
                appManager.JobsManager.CreateTriggeredJobAsync("job2", zippedJobBinaries).Wait();
                appManager.JobsManager.CreateContinuousJobAsync("job1", zippedJobBinaries).Wait();
                appManager.JobsManager.CreateContinuousJobAsync("job2", zippedJobBinaries).Wait();

                // Disabling a continuous job should not affect the job count
                WaitUntilAssertVerified(
                    "disable continuous job",
                    TimeSpan.FromSeconds(60),
                    () => appManager.JobsManager.DisableContinuousJobAsync("job1").Wait());

                // Adding a setting to a triggered job should not affect the job count
                var jobSettings = new JobSettings();
                jobSettings["one"] = 1;
                appManager.JobsManager.SetTriggeredJobSettingsAsync("job1", jobSettings).Wait();

                var triggeredJobs = appManager.JobsManager.ListTriggeredJobsAsync().Result;
                var continuousJobs = appManager.JobsManager.ListContinuousJobsAsync().Result;

                Assert.Equal(2, triggeredJobs.Count());
                Assert.Equal(2, continuousJobs.Count());

                VerifyTriggeredJobTriggers(appManager, "job1", 1, "Success", "echo ");

                appManager.JobsManager.DeleteTriggeredJobAsync("job1").Wait();
                appManager.JobsManager.DeleteTriggeredJobAsync("job2").Wait();
                appManager.JobsManager.DeleteContinuousJobAsync("job1").Wait();
                appManager.JobsManager.DeleteContinuousJobAsync("job2").Wait();

                triggeredJobs = appManager.JobsManager.ListTriggeredJobsAsync().Result;
                continuousJobs = appManager.JobsManager.ListContinuousJobsAsync().Result;

                Assert.Equal(0, triggeredJobs.Count());
                Assert.Equal(0, continuousJobs.Count());
            });
        }

        private void VerifyTriggeredJobTriggers(ApplicationManager appManager, string jobName, int expectedNumberOfRuns, string expectedStatus, string expectedOutput = null, string expectedError = null, string arguments = null)
        {
            appManager.JobsManager.InvokeTriggeredJobAsync(jobName, arguments).Wait();

            WaitUntilAssertVerified(
                "verify triggered job run",
                TimeSpan.FromSeconds(20),
                () =>
                {
                    TriggeredJobHistory triggeredJobHistory = appManager.JobsManager.GetTriggeredJobHistoryAsync(jobName).Result;
                    Assert.NotNull(triggeredJobHistory);
                    Assert.Equal(expectedNumberOfRuns, triggeredJobHistory.TriggeredJobRuns.Count());

                    TriggeredJobRun triggeredJobRun = triggeredJobHistory.TriggeredJobRuns.FirstOrDefault();
                    AssertTriggeredJobRun(appManager, triggeredJobRun, jobName, expectedStatus, expectedOutput, expectedError);
                });
        }

        private async Task VerifyTriggeredJobDoesNotTrigger(ApplicationManager appManager, string jobName)
        {
            HttpUnsuccessfulRequestException thrownException = null;
            try
            {
                await appManager.JobsManager.InvokeTriggeredJobAsync(jobName);
            }
            catch (HttpUnsuccessfulRequestException ex)
            {
                thrownException = ex;
            }

            Assert.NotNull(thrownException);
            Assert.Equal(HttpStatusCode.Conflict, thrownException.ResponseMessage.StatusCode);
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
                    "run.ps1",
                    "run.sh",
                    //"run.py",
                    //"run.php",
                    "run.js",
                    "go.cmd",
                    "do.bat",
                    "console.exe",
                    "execute.ps1",
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

        private static void RestartServiceSite(ApplicationManager appManager)
        {
            try
            {
                appManager.ProcessManager.KillProcessAsync(0).Wait();
            }
            catch
            {
            }
        }

        private void VerifyContinuousJobEnabled(ApplicationManager appManager)
        {
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

        private void VerifyContinuousJobDisabled(ApplicationManager appManager)
        {
            WaitUntilAssertVerified(
                "continuous job disabled",
                TimeSpan.FromSeconds(40),
                () =>
                {
                    var jobs = appManager.JobsManager.ListContinuousJobsAsync().Result;
                    Assert.Equal(1, jobs.Count());
                    Assert.Equal("Stopped", jobs.First().Status);
                });

            WaitUntilAssertVerified(
                "make sure process is down",
                TimeSpan.FromSeconds(40),
                () =>
                {
                    var allProcesses = appManager.ProcessManager.GetProcessesAsync().Result;
                    var process = allProcesses.FirstOrDefault(p => String.Equals("ConsoleWorker", p.Name, StringComparison.OrdinalIgnoreCase));
                    Assert.Null(process);
                });
        }

        private string BuildZippedJobBinaries()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            string innerTempDirectory = Path.Combine(tempDirectory, "inner");
            Directory.CreateDirectory(innerTempDirectory);

            string zippedFilePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".zip");
            const string scriptFileName1 = "run.cmd";
            const string scriptFileContent1 = "test.cmd\n";
            var scriptFilePath1 = Path.Combine(innerTempDirectory, scriptFileName1);
            File.WriteAllText(scriptFilePath1, scriptFileContent1);

            const string scriptFileName2 = "test.cmd";
            const string scriptFileContent2 = "echo test";
            var scriptFilePath2 = Path.Combine(innerTempDirectory, scriptFileName2);
            File.WriteAllText(scriptFilePath2, scriptFileContent2);

            ZipFile.CreateFromDirectory(tempDirectory, zippedFilePath);

            return zippedFilePath;
        }

        private void VerifyVerificationFile(ApplicationManager appManager, string[] expectedContentLines)
        {
            var logFiles = appManager.VfsManager.ListAsync("LogFiles").Result;
            string[] verificationFileNames = logFiles.Where(logFile => logFile.Name.StartsWith("verification.txt", StringComparison.OrdinalIgnoreCase)).Select(logFile => logFile.Name).ToArray();

            for (int fileIndex = 0; fileIndex < verificationFileNames.Length; fileIndex++)
            {
                // Make sure at least one file is verified
                try
                {
                    string verificationFileName = verificationFileNames[fileIndex];
                    string verificationFileContent = appManager.VfsManager.ReadAllText("LogFiles/" + verificationFileName).TrimEnd();
                    Assert.NotNull(verificationFileContent);
                    string[] lines = verificationFileContent.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                    Assert.Equal(expectedContentLines.Length, lines.Length);
                    for (int i = 0; i < expectedContentLines.Length; i++)
                    {
                        Assert.Equal(expectedContentLines[i], lines[i].Trim());
                    }

                    return;
                }
                catch
                {
                    if (fileIndex == verificationFileNames.Length - 1)
                    {
                        throw;
                    }
                }
            }
        }

        private void AssertTriggeredJobRun(ApplicationManager appManager, TriggeredJobRun actualTriggeredJobRun, string expectedJobName, string expectedStatus, string expectedOutput = null, string expectedError = null)
        {
            Assert.NotNull(actualTriggeredJobRun);
            Assert.Equal(expectedJobName, actualTriggeredJobRun.JobName);
            Assert.Equal(expectedStatus, actualTriggeredJobRun.Status);
            Assert.NotNull(actualTriggeredJobRun.Duration);
            Assert.NotNull(actualTriggeredJobRun.EndTime);
            Assert.NotNull(actualTriggeredJobRun.Id);
            Assert.NotNull(actualTriggeredJobRun.StartTime);
            Assert.NotNull(actualTriggeredJobRun.Url);

            AssertUrlContentAsync(appManager, actualTriggeredJobRun.OutputUrl, expectedOutput).Wait();
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

                    Assert.True(content.IndexOf(expectedContent, StringComparison.OrdinalIgnoreCase) >= 0, "Expected content: " + expectedContent);
                }
            }
        }

        private void RunScenario(string name, Action<ApplicationManager> action)
        {
            ApplicationManager.Run(name, appManager =>
            {
                appManager.SettingsManager.SetValue(SettingsKeys.WebJobsRestartTime, "5").Wait();

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
                    appManager.VfsManager.Delete(JobsBinPath, recursive: true);
                    appManager.VfsManager.Delete(JobsDataPath, recursive: true);

                    var logFiles = appManager.VfsManager.ListAsync("LogFiles").Result;
                    foreach (var logFile in logFiles)
                    {
                        if (logFile.Name.StartsWith("appSettings.txt", StringComparison.OrdinalIgnoreCase) ||
                            logFile.Name.StartsWith("verification.txt", StringComparison.OrdinalIgnoreCase))
                        {
                            appManager.VfsManager.Delete("LogFiles/" + logFile.Name);
                        }
                    }
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