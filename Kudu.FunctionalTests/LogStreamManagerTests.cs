using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Kudu.Client.Infrastructure;
using Kudu.FunctionalTests.Infrastructure;
using Kudu.TestHarness;
using Xunit;

namespace Kudu.FunctionalTests
{
    public class LogStreamManagerTests
    {
        [Fact]
        public void TestLogStreamBasic()
        {
            string appName = KuduUtils.GetRandomWebsiteName("TestLogStreamBasic");

            ApplicationManager.Run(appName, appManager =>
            {
                string logFilePath = Path.Combine(PathHelper.SitesPath, appName, "logFiles");
                string txtFile = Path.Combine(logFilePath, "temp.txt");
                if (File.Exists(txtFile))
                {
                    File.Delete(txtFile);
                }
                string logFile = Path.Combine(logFilePath, "temp.log");
                if (File.Exists(logFile))
                {
                    File.Delete(logFile);
                }
                string xmlFile = Path.Combine(logFilePath, "temp.xml");
                if (File.Exists(xmlFile))
                {
                    File.Delete(xmlFile);
                }

                RemoteLogStreamManager manager = new RemoteLogStreamManager(appManager.ServiceUrl + "/logstream");

                using (LogStreamWaitHandle waitHandle = new LogStreamWaitHandle(manager.GetStream().Result))
                {
                    string line = waitHandle.WaitNextLine(10000);
                    Assert.True(!string.IsNullOrEmpty(line) && line.StartsWith("Welcome", StringComparison.OrdinalIgnoreCase), "check welcome message: " + line);

                    string content = Guid.NewGuid().ToString();
                    File.WriteAllLines(txtFile, new string[] { content });
                    line = waitHandle.WaitNextLine(10000);
                    Assert.Equal(content, line);

                    content = Guid.NewGuid().ToString();
                    File.WriteAllLines(logFile, new string[] { content });
                    line = waitHandle.WaitNextLine(10000);
                    Assert.Equal(content, line);

                    // write to xml file, we should not get any live stream
                    content = Guid.NewGuid().ToString();
                    File.WriteAllLines(xmlFile, new string[] { content });
                    line = waitHandle.WaitNextLine(1000);
                    Assert.True(line == null, "no more message: " + line);
                }
            });
        }

        [Fact]
        public void TestLogStreamSubFolder()
        {
            string appName = KuduUtils.GetRandomWebsiteName("TestLogStreamFilter");

            ApplicationManager.Run(appName, appManager =>
            {
                List<string> logFiles = new List<string>();
                List<LogStreamWaitHandle> waitHandles = new List<LogStreamWaitHandle>();
                for (int i = 0; i < 2; ++i)
                {
                    string logFilePath = Path.Combine(PathHelper.SitesPath, appName, "logFiles", "folder" + i);
                    if (!Directory.Exists(logFilePath))
                    {
                        Directory.CreateDirectory(logFilePath);
                    }
                    string logFile = Path.Combine(logFilePath, "temp.txt");
                    if (File.Exists(logFile))
                    {
                        File.Delete(logFile);
                    }
                    logFiles.Add(logFile);

                    RemoteLogStreamManager mgr = new RemoteLogStreamManager(appManager.ServiceUrl + "/logstream/folder" + i);
                    LogStreamWaitHandle waitHandle = new LogStreamWaitHandle(mgr.GetStream().Result);
                    string line = waitHandle.WaitNextLine(10000);
                    Assert.True(!string.IsNullOrEmpty(line) && line.StartsWith("Welcome", StringComparison.OrdinalIgnoreCase), "check welcome message: " + line);
                    waitHandles.Add(waitHandle);
                }

                RemoteLogStreamManager manager = new RemoteLogStreamManager(appManager.ServiceUrl + "/logstream");

                using (LogStreamWaitHandle waitHandle = new LogStreamWaitHandle(manager.GetStream().Result))
                {
                    try
                    {
                        string line = waitHandle.WaitNextLine(10000);
                        Assert.True(!string.IsNullOrEmpty(line) && line.StartsWith("Welcome", StringComparison.OrdinalIgnoreCase), "check welcome message: " + line);

                        // write to folder0, we should not get any live stream for folder1 listener
                        string content = Guid.NewGuid().ToString();
                        File.WriteAllLines(logFiles[0], new string[] { content });
                        line = waitHandle.WaitNextLine(10000);
                        Assert.Equal(content, line);
                        line = waitHandles[0].WaitNextLine(10000);
                        Assert.Equal(content, line);
                        line = waitHandles[1].WaitNextLine(1000);
                        Assert.True(line == null, "no more message: " + line);

                        // write to folder1, we should not get any live stream for folder0 listener
                        content = Guid.NewGuid().ToString();
                        File.WriteAllLines(logFiles[1], new string[] { content });
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
            string appName = KuduUtils.GetRandomWebsiteName("TestLogStreamNotFound");

            ApplicationManager.Run(appName, appManager =>
            {
                RemoteLogStreamManager manager = new RemoteLogStreamManager(appManager.ServiceUrl + "/logstream/notfound");
                var ex = KuduAssert.ThrowsUnwrapped<WebException>(() => manager.GetStream().Wait());
                Assert.Equal(((HttpWebResponse)ex.Response).StatusCode, HttpStatusCode.NotFound);
            });
        }

        // This is a test class current workaround Stream.Close hangs.
        class RemoteLogStreamManager : KuduRemoteClientBase
        {
            public RemoteLogStreamManager(string serviceUrl)
                : base(serviceUrl)
            {
            }

            public Task<Stream> GetStream()
            {
                HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(ServiceUrl);
                TaskCompletionSource<Stream> tcs = new TaskCompletionSource<Stream>();
                RequestState state = new RequestState { Manager = this, TaskCompletionSource = tcs, Request = request };

                if (this._client.DefaultRequestHeaders.Authorization != null)
                {
                    request.Headers["Authorization"] = this._client.DefaultRequestHeaders.Authorization.Scheme + " " + this._client.DefaultRequestHeaders.Authorization.Parameter;
                }

                IAsyncResult result = request.BeginGetResponse(RemoteLogStreamManager.OnGetResponse, state);
                if (result.CompletedSynchronously)
                {
                    state.Response = (HttpWebResponse)request.EndGetResponse(result);
                    OnGetResponse(state);
                }

                return tcs.Task;
            }

            private static void OnGetResponse(IAsyncResult result)
            {
                RequestState state = (RequestState)result.AsyncState;
                try
                {
                    state.Response = (HttpWebResponse)state.Request.EndGetResponse(result);
                    state.Manager.OnGetResponse(state);
                }
                catch (Exception ex)
                {
                    state.TaskCompletionSource.TrySetException(ex);
                }
            }

            private void OnGetResponse(RequestState state)
            {
                state.TaskCompletionSource.TrySetResult(new DelegatingStream(state.Response.GetResponseStream(), state));
            }

            class RequestState
            {
                public RemoteLogStreamManager Manager { get; set; }
                public TaskCompletionSource<Stream> TaskCompletionSource { get; set; }
                public HttpWebRequest Request { get; set; }
                public HttpWebResponse Response { get; set; }
            }

            class DelegatingStream : Stream
            {
                Stream inner;
                RequestState state;

                public DelegatingStream(Stream inner, RequestState state)
                {
                    this.inner = inner;
                    this.state = state;
                }

                public override void Close()
                {
                    // To avoid hanging!
                    this.state.Request.Abort();

                    this.inner.Close();
                }

                public override bool CanRead
                {
                    get { return this.inner.CanRead; }
                }

                public override bool CanSeek
                {
                    get { return this.inner.CanSeek; }
                }

                public override bool CanWrite
                {
                    get { return this.inner.CanWrite; }
                }

                public override void Flush()
                {
                    this.inner.Flush();
                }

                public override long Length
                {
                    get { return this.inner.Length; }
                }

                public override long Position
                {
                    get { return this.inner.Position; }
                    set { this.inner.Position = value; }
                }

                public override int Read(byte[] buffer, int offset, int count)
                {
                    return this.inner.Read(buffer, offset, count);
                }

                public override long Seek(long offset, SeekOrigin origin)
                {
                    return this.inner.Seek(offset, origin);
                }

                public override void SetLength(long value)
                {
                    this.inner.SetLength(value);
                }

                public override void Write(byte[] buffer, int offset, int count)
                {
                    this.inner.Write(buffer, offset, count);
                }
            }
        }

        class LogStreamWaitHandle : IDisposable
        {
            Stream stream;
            List<string> lines;
            Semaphore sem;
            ManualResetEvent disposed = new ManualResetEvent(false);

            public LogStreamWaitHandle(Stream stream)
            {
                this.stream = stream;
                this.lines = new List<string>();
                this.sem = new Semaphore(0, Int32.MaxValue);
                Task.Factory.StartNew(() =>
                {
                    try
                    {
                        using (StreamReader reader = new StreamReader(stream))
                        {
                            bool initial = true;
                            while (!reader.EndOfStream)
                            {
                                string line = reader.ReadLine();
                                if (line != null)
                                {
                                    if (initial)
                                    {
                                        // accommodate for gap between first welcome and event hookup
                                        Thread.Sleep(1000);
                                        initial = false;
                                    }
                                    
                                    lock (lines)
                                    {
                                        lines.Add(line);
                                        this.sem.Release();
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                    }
                    finally
                    {
                        disposed.Set();
                    }
                });
            }

            public void Dispose()
            {
                this.stream.Close();
                this.disposed.WaitOne(10000);
            }

            public string WaitNextLine(int millisecs)
            {
                if (this.sem.WaitOne(millisecs))
                {
                    lock (lines)
                    {
                        string result = lines[0];
                        lines.RemoveAt(0);
                        return result;
                    }
                }

                return null;
            }
        }
    }
}
