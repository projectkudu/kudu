using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Web;
using System.Xml;
using System.Xml.Linq;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;
using Moq;
using Xunit;

namespace Kudu.Core.Test.Tracing
{
    public class XmlTracerTests
    {
        [Fact]
        public void TracerMaxXmlFilesTest()
        {
            // Mock
            var path = @"x:\kudu\trace";
            var tracer = new XmlTracer(path, TraceLevel.Verbose);
            FileSystemHelpers.Instance = GetMockFileSystem();

            try
            {
                var total = XmlTracer.MaxXmlFiles + 10;
                for (int i = 0; i < total; ++i)
                {
                    tracer.Trace(Guid.NewGuid().ToString(), new Dictionary<string, string>());
                }

                var files = FileSystemHelpers.GetFiles(path, "*.xml");
                Assert.Equal(total, files.Length);

                // wait till interval and write another trace
                typeof(XmlTracer).GetField("_lastCleanup", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, DateTime.MinValue);
                tracer.Trace(Guid.NewGuid().ToString(), new Dictionary<string, string>());

                files = FileSystemHelpers.GetFiles(path, "*.xml");
                Assert.True(files.Length < XmlTracer.MaxXmlFiles);
            }
            catch (Exception)
            {
                FileSystemHelpers.Instance = null;
                throw;
            }
        }

        [Theory]
        [MemberData("Requests")]
        public void TracerRequestsTest(TraceLevel traceLevel, RequestInfo[] requests)
        {
            // Mock
            var path = @"x:\kudu\trace";
            var tracer = new XmlTracer(path, traceLevel);
            FileSystemHelpers.Instance = GetMockFileSystem();

            try
            {
                foreach (var request in requests)
                {
                    Dictionary<string, string> attribs = new Dictionary<string, string>
                    {
                        { "type", "request" },
                        { "url", request.Url },
                        { "method", "GET" },
                        { "statusCode", ((int)request.StatusCode).ToString() },
                    };

                    using (tracer.Step("Incoming Request", attribs))
                    {
                        tracer.Trace("Outgoing response", attribs);
                    }
                }

                var traces = new List<RequestInfo>();
                foreach (var file in FileSystemHelpers.GetFiles(path, "*s.xml").OrderBy(n => n))
                {
                    var document = XDocument.Load(FileSystemHelpers.OpenRead(file));
                    var trace = new RequestInfo { Url = document.Root.Attribute("url").Value };
                    var elapsed = document.Root.Nodes().Last();

                    // Assert
                    Assert.Equal(XmlNodeType.Comment, elapsed.NodeType);
                    Assert.Contains("duration:", ((XComment)elapsed).Value);

                    traces.Add(trace);
                }

                // Assert
                Assert.Equal(requests.Where(r => r.Traced), traces, RequestInfoComparer.Instance);
            }
            catch (Exception)
            {
                FileSystemHelpers.Instance = null;
                throw;
            }
        }

        [Theory]
        [MemberData("TraceRequests")]
        public void TraceFiltersTests(bool expected, TraceLevel level, TraceInfo info, int statusCode)
        {
            Assert.Equal(expected, XmlTracer.ShouldTrace(level, info, statusCode));
        }

        public static IEnumerable<object[]> TraceRequests
        {
            get
            {
                // trace verbose
                yield return new object[] 
                { 
                    true, 
                    TraceLevel.Verbose,
                    null, 
                    200
                };

                // trace non get request
                yield return new object[] 
                { 
                    true, 
                    TraceLevel.Error,
                    new TraceInfo("title", new Dictionary<string, string>()), 
                    200
                };

                // trace non get request
                yield return new object[] 
                { 
                    true, 
                    TraceLevel.Error,
                    new TraceInfo("title", new Dictionary<string, string>
                        {
                            { "type", "request" },
                            { "method", "POST" },
                            { "url", "/api/webjobs" },
                        }), 
                    200
                };

                // trace non success request
                yield return new object[] 
                { 
                    true, 
                    TraceLevel.Error,
                    new TraceInfo("title", new Dictionary<string, string>
                        {
                            { "type", "request" },
                            { "method", "GET" },
                            { "url", "/api/webjobs" },
                        }), 
                    500
                };

                // filter NotModified
                yield return new object[] 
                { 
                    false, 
                    TraceLevel.Error,
                    new TraceInfo("title", new Dictionary<string, string>
                        {
                            { "type", "request" },
                            { "method", "GET" },
                            { "url", "/api/settings" },
                        }), 
                    304
                };

                // filter success
                yield return new object[] 
                { 
                    false, 
                    TraceLevel.Error,
                    new TraceInfo("title", new Dictionary<string, string>
                        {
                            { "type", "request" },
                            { "method", "GET" },
                            { "url", "/api/webjobs" },
                        }), 
                    200
                };

                // filter 
                yield return new object[] 
                { 
                    false, 
                    TraceLevel.Error,
                    new TraceInfo("title", new Dictionary<string, string>
                        {
                            { "type", "request" },
                            { "method", "GET" },
                            { "url", "/api/siteextensions?a=b" },
                        }), 
                    200
                };

                // filter success
                yield return new object[] 
                { 
                    false, 
                    TraceLevel.Error,
                    new TraceInfo("title", new Dictionary<string, string>
                        {
                            { "type", "request" },
                            { "method", "GET" },
                            { "url", "/api/processes" },
                        }), 
                    200
                };

                // filter success
                yield return new object[] 
                { 
                    false, 
                    TraceLevel.Error,
                    new TraceInfo("title", new Dictionary<string, string>
                        {
                            { "type", "request" },
                            { "method", "GET" },
                            { "url", "/api/deployments?a=b" },
                        }), 
                    200
                };
            }
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

        private IFileSystem GetMockFileSystem()
        {
            var files = new Dictionary<string, MemoryStream>();
            var fs = new Mock<IFileSystem>(MockBehavior.Strict);
            var fileBase = new Mock<FileBase>(MockBehavior.Strict);
            var fileInfoFactoryBase = new Mock<IFileInfoFactory>(MockBehavior.Strict);
            var dirBase = new Mock<DirectoryBase>(MockBehavior.Strict);
            var dirInfoBase = new Mock<DirectoryInfoBase>(MockBehavior.Strict);

            // Setup
            fs.SetupGet(f => f.File)
              .Returns(() => fileBase.Object);
            fs.SetupGet(f => f.FileInfo)
              .Returns(() => fileInfoFactoryBase.Object);
            fs.SetupGet(f => f.Directory)
              .Returns(() => dirBase.Object);

            fileBase.Setup(f => f.Exists(It.IsAny<string>()))
                    .Returns((string path) => files.ContainsKey(path));
            fileBase.Setup(f => f.Open(It.IsAny<string>(), FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
                    .Returns((string path, FileMode fileMode, FileAccess fileAccess, FileShare fileShare) =>
                    {
                        MemoryStream stream;
                        if (!files.TryGetValue(path, out stream))
                        {
                            stream = MockMemoryStream();
                            files[path] = stream;
                        }
                        return stream;
                    });
            fileBase.Setup(f => f.OpenRead(It.IsAny<string>()))
                    .Returns((string path) =>
                    {
                        MemoryStream stream = files[path];
                        stream.Position = 0;
                        return stream;
                    });
            fileBase.Setup(f => f.Move(It.IsAny<string>(), It.IsAny<string>()))
                    .Callback((string sourceFileName, string destFileName) =>
                    {
                        files[destFileName] = files[sourceFileName];
                        files.Remove(sourceFileName);
                    });


            fileInfoFactoryBase.Setup(f => f.FromFileName(It.IsAny<string>()))
                        .Returns((string file) =>
                        {
                            var fileInfoBase = new Mock<FileInfoBase>(MockBehavior.Strict);
                            fileInfoBase.SetupGet(f => f.Exists)
                                        .Returns(() => files.ContainsKey(file));
                            fileInfoBase.SetupSet(f => f.Attributes = It.IsAny<FileAttributes>());
                            fileInfoBase.Setup(f => f.Delete())
                                        .Callback(() => files.Remove(file));
                            return fileInfoBase.Object;
                        });

            dirBase.Setup(d => d.GetFiles(It.IsAny<string>(), It.IsAny<string>()))
                   .Returns(() => files.Keys.ToArray());
            dirBase.Setup(d => d.CreateDirectory(It.IsAny<string>()))
                   .Returns(dirInfoBase.Object);

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