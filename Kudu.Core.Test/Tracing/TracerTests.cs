using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Xml.Linq;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;
using Moq;
using Xunit;
using Xunit.Extensions;

namespace Kudu.Core.Test.Tracing
{
    public class TracerTests
    {
        [Fact]
        public void TracerMaxLogEntriesTest()
        {
            // Mock
            var path = @"x:\git\trace\trace.xml";
            var traceLock = new OperationLockTests.MockOperationLock();
            var threads = 5;
            var tasks = new List<Task>(threads);
            var total = 0;
            FileSystemHelpers.Instance = GetMockFileSystem();

            // Test writing 5*50 traces which > 200 limits
            // also test concurrency and locking with multiple threads
            for (int i = 0; i < threads; i++)
            {
                tasks.Add(Task.Factory.StartNew(() =>
                {
                    var tracer = new Tracer(path, TraceLevel.Verbose, traceLock);

                    for (int j = 0; j < 50; ++j)
                    {
                        tracer.Trace(Guid.NewGuid().ToString(), new Dictionary<string, string>());
                        Interlocked.Increment(ref total);
                    }
                }));
            }

            Task.WaitAll(tasks.ToArray());

            XDocument document = ReadTraceFile(path);

            // Assert
            Assert.True(total > Tracer.MaxLogEntries);
            Assert.Equal(Tracer.MaxLogEntries, document.Root.Elements().Count());
        }

        [Theory]
        [PropertyData("Requests")]
        public void TracerRequestsTest(TraceLevel traceLevel, RequestInfo[] requests)
        {
            // Mock
            var path = @"x:\git\trace\trace.xml";
            var traceLock = new OperationLockTests.MockOperationLock();
            var tracer = new Tracer(path, traceLevel, traceLock);
            FileSystemHelpers.Instance = GetMockFileSystem();

            // Test
            IntializeTraceFile(path);

            foreach (var request in requests)
            {
                Dictionary<string, string> attribs = new Dictionary<string, string>
                {
                    { "url", request.Url },
                    { "statusCode", ((int)request.StatusCode).ToString() },
                };

                using (tracer.Step("Incoming Request", attribs))
                {
                    tracer.Trace("Outgoing response", attribs);
                }
            }

            XDocument document = ReadTraceFile(path);
            IEnumerable<RequestInfo> traces = document.Root.Elements().Select(e => new RequestInfo { Url = e.Attribute("url").Value });

            // Assert
            Assert.Equal(requests.Where(r => r.Traced), traces, RequestInfoComparer.Instance);
        }

        [Theory]
        [InlineData(true, TraceExtensions.AlwaysTrace)]
        [InlineData(true, TraceExtensions.TraceLevelKey)]
        [InlineData(true, "Max-Forwards")]
        [InlineData(true, "X-ARR-LOG-ID")]
        [InlineData(false, "url")]
        [InlineData(false, "method")]
        [InlineData(false, "type")]
        [InlineData(false, "Host")]
        public void TracingAttributeBlacklistTests(bool expected, string header)
        {
            Assert.Equal(expected, TraceExtensions.IsNonDisplayableAttribute(header));
        }

        [Theory]
        [InlineData(true, "/", null)]
        [InlineData(true, "/", "Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/31.0.1650.63 Safari/537.36")]
        [InlineData(true, "/vfs/", "Mozilla/5.0 (Windows NT 6.3; WOW64; Trident/7.0; rv:11.0) like Gecko")]
        [InlineData(false, "/coolapp.git/info/refs?service=git-receive-pack", "git/1.8.4.msysgit.0")]
        [InlineData(false, "/dump", null)]
        [InlineData(false, "/scm/info", null)]
        public void SkipRequestTracingTests(bool expected, string rawUrl, string userAgent)
        {
            var mock = new Mock<HttpRequestBase>();
            mock.Setup(req => req.RawUrl).Returns(rawUrl);
            mock.Setup(req => req.UserAgent).Returns(userAgent);
            HttpRequestBase request = mock.Object;
            Assert.Equal(expected, TraceExtensions.ShouldSkipRequest(request));
        }

        public static IEnumerable<object[]> Requests
        {
            get
            {
                // Trace level is Off, expect no traces regardless of status
                yield return new object[] { TraceLevel.Off, new[]
                {
                    new RequestInfo { Url = "/any1", Traced = false },
                    new RequestInfo { Url = "/any2", StatusCode = HttpStatusCode.BadRequest, Traced = false },
                }};

                // Trace level is Verbose, expect all traces
                yield return new object[] { TraceLevel.Verbose, new[]
                {
                    new RequestInfo { Url = "/any1" },
                    new RequestInfo { Url = "/deployments?a=1" },
                    new RequestInfo { Url = "/any2" },
                    new RequestInfo { Url = "/deployments?a=2", StatusCode = HttpStatusCode.NotModified },
                }};

                // Trace level is Error, expect all traces except /deployments NotModified
                yield return new object[] { TraceLevel.Error, new[]
                {
                    new RequestInfo { Url = "/any1" },
                    new RequestInfo { Url = "/deployments?a=1" },
                    new RequestInfo { Url = "/any2" },
                    new RequestInfo { Url = "/deployments?a=2", StatusCode = HttpStatusCode.NotModified, Traced = false },
                    new RequestInfo { Url = "/vfs/any3", StatusCode = HttpStatusCode.NotModified, Traced = false },
                }};
            }
        }

        private void IntializeTraceFile(string path)
        {
            using (var writer = new StreamWriter(FileSystemHelpers.OpenFile(path, FileMode.Create, FileAccess.Write, FileShare.Read)))
            {
                writer.WriteLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
                writer.WriteLine("<trace/>");
            }
        }

        private XDocument ReadTraceFile(string path)
        {
            using (var stream = FileSystemHelpers.OpenRead(path))
            {
                return XDocument.Load(stream);
            }
        }

        private IFileSystem GetMockFileSystem()
        {
            var files = new Dictionary<string, MemoryStream>();
            var fs = new Mock<IFileSystem>(MockBehavior.Strict);
            var fileBase = new Mock<FileBase>(MockBehavior.Strict);
            var dirBase = new Mock<DirectoryBase>(MockBehavior.Strict);
            var dirInfoBase = new Mock<DirectoryInfoBase>(MockBehavior.Strict);

            // Setup
            fs.Setup(f => f.File)
              .Returns(fileBase.Object);
            fs.Setup(f => f.Directory)
              .Returns(dirBase.Object);

            fileBase.Setup(f => f.Exists(It.IsAny<string>()))
                    .Returns((string path) => files.ContainsKey(path));
            fileBase.Setup(f => f.Open(It.IsAny<string>(), FileMode.Create, FileAccess.Write, FileShare.Read))
                    .Returns((string path, FileMode fileMode, FileAccess fileAccess, FileShare fileShare) =>
                    {
                        var stream = MockMemoryStream();
                        files[path] = stream;
                        return stream;
                    });
            fileBase.Setup(f => f.OpenRead(It.IsAny<string>()))
                    .Returns((string path) =>
                    {
                        MemoryStream stream = files[path];
                        stream.Position = 0;
                        return stream;
                    });

            dirBase.Setup(d => d.CreateDirectory(It.IsAny<string>()))
                   .Returns(dirInfoBase.Object);

            FileSystemHelpers.Instance = fs.Object;

            return fs.Object;
        }

        private MemoryStream MockMemoryStream()
        {
            // Override Close with no-op
            var stream = new Mock<MemoryStream> { CallBase = true };
            stream.Setup(s => s.Close());
            return stream.Object;
        }

        public class RequestInfo
        {
            public RequestInfo()
            {
                StatusCode = HttpStatusCode.OK;
                Traced = true;
            }

            public string Url { get; set; }
            public HttpStatusCode StatusCode { get; set; }
            public bool Traced { get; set; }
        }

        public class RequestInfoComparer : IEqualityComparer<RequestInfo>
        {
            public static RequestInfoComparer Instance = new RequestInfoComparer();

            public bool Equals(RequestInfo x, RequestInfo y)
            {
                return x.Url == y.Url;
            }

            public int GetHashCode(RequestInfo obj)
            {
                return obj.Url.GetHashCode();
            }
        }
    }
}