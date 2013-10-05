using System;
using System.Linq;
using System.Threading;
using Kudu.Core.Deployment;
using Kudu.FunctionalTests.Infrastructure;
using Kudu.TestHarness;
using Xunit;

namespace Kudu.FunctionalTests
{
    public class ConsoleWorkerTests
    {
        private const string VerificationFilePath = "LogFiles/verification.txt";
        private const string RunScriptPath = "Site/wwwroot/bin/run_worker.cmd";
        private const string RunningVerification = "Process went down, Waiting for";
        private const string StoppedVerification = "Missing run_worker.cmd file";
        private const string ExpectedDotNetVerificationFileContent = "Verified!!!";
        private const string ExpectedChangedFileContent = "Changed!!!";

        [Fact]
        public void PushAndDeployDotNetConsoleWorker()
        {
            ApplicationManager.Run("ConsoleWorker", appManager =>
            {
                using (TestRepository testRepository = Git.Clone("ConsoleWorker"))
                {
                    ///////// Part 1
                    TestTracer.Trace("I) Starting ConsoleWorker test, deploying the worker");

                    DeployConsoleWorker(appManager, testRepository, ExpectedDotNetVerificationFileContent);

                    ///////// Part 2
                    TestTracer.Trace("II) Waiting for worker to start the process again...");

                    var lines = new string[0];
                    for (int checkCount = 0; lines.Length < 2 && checkCount < 60; checkCount++)
                    {
                        Thread.Sleep(1000);
                        string verificationFileContent = appManager.VfsManager.ReadAllText(VerificationFilePath);
                        lines = verificationFileContent.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    }

                    Assert.Equal(2, lines.Length);
                    Assert.Equal(ExpectedDotNetVerificationFileContent, lines[1]);

                    ///////// Part 3
                    TestTracer.Trace("III) Verifying worker stopped when run_worker.cmd is missing");

                    appManager.VfsManager.Delete(RunScriptPath);

                    bool workerStopped = false;
                    for (int checkCount = 0; !workerStopped && checkCount < 20; checkCount++)
                    {
                        try
                        {
                            Thread.Sleep(1000);
                            KuduAssert.VerifyUrl(appManager.SiteUrl, StoppedVerification);
                            workerStopped = true;
                        }
                        catch (Exception)
                        {
                        }
                    }
                    KuduAssert.VerifyUrl(appManager.SiteUrl, StoppedVerification);

                    ///////// Part 4
                    TestTracer.Trace("IV) Verifying worker starts again when run.cmd is back");

                    appManager.VfsManager.WriteAllText(RunScriptPath, "ConsoleWorker.exe");

                    bool workerStarted = false;
                    for (int checkCount = 0; !workerStarted && checkCount < 60; checkCount++)
                    {
                        try
                        {
                            Thread.Sleep(1000);
                            KuduAssert.VerifyUrl(appManager.SiteUrl, RunningVerification);
                            workerStarted = true;
                        }
                        catch (Exception)
                        {
                        }
                    }
                    KuduAssert.VerifyUrl(appManager.SiteUrl, RunningVerification);

                    ///////// Part 5
                    TestTracer.Trace("V) Make sure redeploy works for console worker");
                    testRepository.Replace("ConsoleWorker\\Program.cs", ExpectedDotNetVerificationFileContent, ExpectedChangedFileContent);
                    Git.Commit(testRepository.PhysicalPath, "Made a small change");

                    appManager.GitDeploy(testRepository.PhysicalPath);

                    DeployConsoleWorker(appManager, testRepository, ExpectedChangedFileContent, expectedDeployments: 2);
                }
            });
        }

        [Fact]
        public void PushAndDeployBasicConsoleWorker()
        {
            ApplicationManager.Run("BasicConsoleWorker", appManager =>
            {
                using (TestRepository testRepository = Git.Clone("BasicConsoleWorker"))
                {
                    ///////// Part 1
                    TestTracer.Trace("I) Starting BasicConsoleWorker test, deploying the worker");

                    DeployConsoleWorker(appManager, testRepository, "RunBasic!!!");

                    ///////// Part 2
                    TestTracer.Trace("II) Add project setting");
                    testRepository.WriteFile(".deployment", @"[config]
PROJECT=subFolder
WORKER_COMMAND=runProject.cmd");
                    Git.Commit(testRepository.PhysicalPath, "Added project");

                    appManager.GitDeploy(testRepository.PhysicalPath);

                    DeployConsoleWorker(appManager, testRepository, "ProjectBasic!!!", expectedDeployments: 2);

                    ///////// Part 3
                    TestTracer.Trace("III) Sub folder command");
                    testRepository.WriteFile(".deployment", @"[config]
WORKER_COMMAND=subFolder\runSubFolder.cmd");
                    Git.Commit(testRepository.PhysicalPath, "Updated to subfolder script");

                    appManager.GitDeploy(testRepository.PhysicalPath);

                    DeployConsoleWorker(appManager, testRepository, "SubFolderBasic!!!", expectedDeployments: 3);
                }
            });
        }

        private void DeployConsoleWorker(ApplicationManager appManager, TestRepository testRepository, string expectedVerificationFileContent, int expectedDeployments = 1)
        {
            appManager.GitDeploy(testRepository.PhysicalPath);
            var results = appManager.DeploymentManager.GetResultsAsync().Result.ToList();

            Assert.Equal(expectedDeployments, results.Count);
            for (int i = 0; i < expectedDeployments; i++)
            {
                Assert.Equal(DeployStatus.Success, results[i].Status);
            }

            // Make sure verification file doesn't exist
            appManager.VfsManager.Delete(VerificationFilePath);

            bool workerWaiting = false;
            for (int checkCount = 0; !workerWaiting && checkCount < 60; checkCount++)
            {
                try
                {
                    Thread.Sleep(1000);
                    KuduAssert.VerifyUrl(appManager.SiteUrl, RunningVerification);
                    workerWaiting = true;
                }
                catch
                {
                }
            }
            KuduAssert.VerifyUrl(appManager.SiteUrl, RunningVerification);

            TestTracer.Trace("Waiting for the verification file...");

            bool verificationFileExists = false;
            for (int checkCount = 0; !verificationFileExists && checkCount < 10; checkCount++)
            {
                Thread.Sleep(500);
                verificationFileExists = appManager.VfsManager.Exists(VerificationFilePath);
            }

            string verificationFileContent = appManager.VfsManager.ReadAllText(VerificationFilePath);
            Assert.Equal(expectedVerificationFileContent, verificationFileContent.TrimEnd());
        }
    }
}