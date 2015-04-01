using System;
using System.Collections.Generic;
using System.Net;
using Kudu.FunctionalTests.Infrastructure;
using Kudu.TestHarness;
using Kudu.TestHarness.Xunit;
using Xunit;

namespace Kudu.FunctionalTests
{
    [KuduXunitTestClass]
    public class LogStreamManagerTests
    {
        [Fact]
        public void TestLogStreamBasic()
        {
            string appName = "TestLogStreamBasic";

            ApplicationManager.Run(appName, appManager =>
            {
                // Act
                using (var localRepo = Git.Clone("LogTester"))
                {
                    appManager.GitDeploy(localRepo.PhysicalPath);
                }

                CreateLogDirectory(appManager.SiteUrl, @"LogFiles\TestLogStreamBasic");

                using (var waitHandle = new LogStreamWaitHandle(appManager.CreateLogStreamManager("TestLogStreamBasic").GetStream().Result))
                {
                    string line = waitHandle.WaitNextLine(10000);
                    Assert.True(!String.IsNullOrEmpty(line) && line.Contains("Welcome"), "check welcome message: " + line);

                    string content = Guid.NewGuid().ToString();
                    WriteLogText(appManager.SiteUrl, @"LogFiles\TestLogStreamBasic\temp.txt", content);
                    line = waitHandle.WaitNextLine(10000);
                    Assert.Equal(content, line);

                    content = Guid.NewGuid().ToString();
                    WriteLogText(appManager.SiteUrl, @"LogFiles\TestLogStreamBasic\temp.log", content);
                    line = waitHandle.WaitNextLine(10000);
                    Assert.Equal(content, line);

                    // write to xml file, we should not get any live stream
                    content = Guid.NewGuid().ToString();
                    WriteLogText(appManager.SiteUrl, @"LogFiles\TestLogStreamBasic\temp.xml", content);
                    line = waitHandle.WaitNextLine(1000);
                    Assert.Null(line);
                }
            });
        }

        [Fact]
        public void TestLogStreamSubFolder()
        {
            string appName = "TestLogStreamFilter";

            ApplicationManager.Run(appName, appManager =>
            {
                // Act
                using (var localRepo = Git.Clone("LogTester"))
                {
                    appManager.GitDeploy(localRepo.PhysicalPath);
                }
                List<string> logFiles = new List<string>();
                List<LogStreamWaitHandle> waitHandles = new List<LogStreamWaitHandle>();
                for (int i = 0; i < 2; ++i)
                {
                    logFiles.Add(@"LogFiles\TestLogStreamFilter\Folder" + i + "\\temp.txt");
                    //Create the directory
                    CreateLogDirectory(appManager.SiteUrl, @"LogFiles\TestLogStreamFilter\Folder" + i);
                    RemoteLogStreamManager mgr = appManager.CreateLogStreamManager("TestLogStreamFilter/folder" + i);
                    var waitHandle = new LogStreamWaitHandle(mgr.GetStream().Result);
                    string line = waitHandle.WaitNextLine(10000);
                    Assert.True(!string.IsNullOrEmpty(line) && line.Contains("Welcome"), "check welcome message: " + line);
                    waitHandles.Add(waitHandle);
                }

                using (LogStreamWaitHandle waitHandle = new LogStreamWaitHandle(appManager.CreateLogStreamManager("TestLogStreamFilter").GetStream().Result))
                {
                    try
                    {
                        string line = waitHandle.WaitNextLine(10000);
                        Assert.True(!string.IsNullOrEmpty(line) && line.Contains("Welcome"), "check welcome message: " + line);

                        // write to folder0, we should not get any live stream for folder1 listener
                        string content = Guid.NewGuid().ToString();
                        WriteLogText(appManager.SiteUrl, logFiles[0], content);
                        line = waitHandle.WaitNextLine(10000);
                        Assert.Equal(content, line);
                        line = waitHandles[0].WaitNextLine(10000);
                        Assert.Equal(content, line);
                        line = waitHandles[1].WaitNextLine(1000);
                        Assert.True(line == null, "no more message: " + line);

                        // write to folder1, we should not get any live stream for folder0 listener
                        content = Guid.NewGuid().ToString();
                        WriteLogText(appManager.SiteUrl, logFiles[1], content);
                        line = waitHandle.WaitNextLine(10000);
                        Assert.Equal(content, line);
                        line = waitHandles[1].WaitNextLine(10000);
                        Assert.Equal(content, line);
                        line = waitHandles[0].WaitNextLine(1000);
                        Assert.True(line == null, "no more message: " + line);
                    }
                    finally
                    {
                        waitHandles[0].Dispose();
                        waitHandles[1].Dispose();
                    }
                }
            });
        }

        [Fact]
        public void TestLogStreamNotFound()
        {
            string appName = "TestLogStreamNotFound";

            ApplicationManager.Run(appName, appManager =>
            {
                RemoteLogStreamManager manager = new RemoteLogStreamManager(appManager.ServiceUrl + "/logstream/notfound");
                var ex = KuduAssert.ThrowsUnwrapped<WebException>(() => manager.GetStream().Wait());
                Assert.Equal(((HttpWebResponse)ex.Response).StatusCode, HttpStatusCode.NotFound);
            });
        }

        private static void WriteLogText(string siteUrl, string filePath, string content)
        {
            string url = String.Format("{0}?path={1}&content={2}", siteUrl, filePath, content);
            KuduAssert.VerifyUrl(url);
        }

        private static void CreateLogDirectory(string siteUrl, string directory)
        {
            if (!directory.EndsWith("\\"))
            {
                directory += "\\";
            }
            string url = String.Format("{0}?path={1}", siteUrl, directory);
            KuduAssert.VerifyUrl(url);
        }
    }
}
