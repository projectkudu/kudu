using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;
using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.Tracing;
using Kudu.Core.Hooks;
using Moq;
using Xunit;

namespace Kudu.Core.Test.Deployment
{
    public class WebHooksManagerTests
    {
        private string _hooksFileContent = String.Empty;
        private WebHooksManager _webHooksManager;

        public WebHooksManagerTests()
        {
            _webHooksManager = BuildWebHooksManager();
        }

        [Fact]
        public void AddWebHookShouldSucceed()
        {
            WebHook[] inputWebHooks = new WebHook[]
            {
                new WebHook(HookEventTypes.PostDeployment, "http://www.gothere.com/aaabbbbbbaaa")
            };

            WebHook[] expectedWebHooks = inputWebHooks;

            IEnumerable<WebHook> webHooksAdded = AddWebHooks(inputWebHooks);

            AssertWebHooks(expectedWebHooks);

            AssertWebHook(expectedWebHooks[0], webHooksAdded.First());
        }

        [Fact]
        public void RemoveWebHookShouldSucceed()
        {
            WebHook[] inputWebHooks = new WebHook[]
            {
                new WebHook(HookEventTypes.PostDeployment, "http://www.gothere.com/aaabbbbbbaaa"),
                new WebHook(HookEventTypes.PostDeployment, "http://www.gothere.com/aaabbbbbbaaa2"),
                new WebHook(HookEventTypes.PostDeployment, "http://www.gothere.com/aaabbbbbbaaa3")
            };

            AddWebHooks(inputWebHooks);

            WebHook webHookToRemove = _webHooksManager.WebHooks.First(h => h.HookAddress == "http://www.gothere.com/aaabbbbbbaaa2");
            _webHooksManager.RemoveWebHook(webHookToRemove.Id);

            WebHook[] expectedWebHooks = new WebHook[]
            {
                new WebHook(HookEventTypes.PostDeployment, "http://www.gothere.com/aaabbbbbbaaa"),
                new WebHook(HookEventTypes.PostDeployment, "http://www.gothere.com/aaabbbbbbaaa3"),
            };
            AssertWebHooks(expectedWebHooks);

            webHookToRemove = _webHooksManager.WebHooks.First(h => h.HookAddress == "http://www.gothere.com/aaabbbbbbaaa");
            _webHooksManager.RemoveWebHook(webHookToRemove.Id);
            expectedWebHooks = new WebHook[]
            {
                new WebHook(HookEventTypes.PostDeployment, "http://www.gothere.com/aaabbbbbbaaa3")
            };
            AssertWebHooks(expectedWebHooks);
        }

        [Fact]
        public void RemoveWebHookForNotFoundAddressShouldSucceed()
        {
            _webHooksManager.RemoveWebHook("http://www.gothere.com/aaabbbbbbaaa2");

            AssertWebHooks(new WebHook[] { });
        }

        [Fact]
        public void Add3WebHooksShouldSucceed()
        {
            WebHook[] inputWebHooks = new WebHook[]
            {
                new WebHook(HookEventTypes.PostDeployment, "http://www.gothere.com/aaabbbbbbaaa"),
                new WebHook(HookEventTypes.PostDeployment, "http://www.gothere.com/aaabbbbbbaaa2", "111", insecureSsl: false),
                new WebHook(HookEventTypes.PostDeployment, "http://www.gothere.com/aaabbbbbbaaa3", "111", insecureSsl: true)
            };

            WebHook[] expectedWebHooks = inputWebHooks;

            AddWebHooks(inputWebHooks);

            AssertWebHooks(expectedWebHooks);
        }

        [Fact]
        public void AddSameAddressWebHookShouldThrowConflictException()
        {
            WebHook[] inputWebHooks = new WebHook[]
            {
                new WebHook(HookEventTypes.PostDeployment, "http://www.gothere.com/aaabbbbbbaaa"),
                new WebHook(HookEventTypes.PostDeployment, "http://www.gothere.com/aaabbbbbbaaa2"),
                new WebHook(HookEventTypes.PostDeployment, "http://www.gothere.com/aaabbbbbbaaa"),
            };

            WebHook[] expectedWebHooks = new WebHook[]
            {
                new WebHook(HookEventTypes.PostDeployment, "http://www.gothere.com/aaabbbbbbaaa"),
                new WebHook(HookEventTypes.PostDeployment, "http://www.gothere.com/aaabbbbbbaaa2"),
            };

            Assert.Throws<ConflictException>(() => AddWebHooks(inputWebHooks));

            AssertWebHooks(expectedWebHooks);
        }

        [Fact]
        public void AddWebHookWithInvalidAddressShouldThrowFormatException()
        {
            Assert.Throws<FormatException>(() =>
                _webHooksManager.AddWebHook(new WebHook(HookEventTypes.PostDeployment, "htsp://\\invalidurl")));
        }

        private void AssertWebHooks(WebHook[] expectedWebHooks)
        {
            IEnumerable<WebHook> webHooks = _webHooksManager.WebHooks;
            Assert.Equal(expectedWebHooks.Length, webHooks.Count());
            for (int i = 0; i < expectedWebHooks.Length; i++)
            {
                WebHook expectedWebHook = expectedWebHooks[i];
                WebHook actualWebHook = webHooks.Single(w => String.Equals(w.HookAddress, expectedWebHook.HookAddress, StringComparison.OrdinalIgnoreCase));
                AssertWebHook(expectedWebHooks[i], actualWebHook);
            }
        }

        private IEnumerable<WebHook> AddWebHooks(WebHook[] webHooks)
        {
            List<WebHook> webHooksAdded = new List<WebHook>();

            foreach (var webHook in webHooks)
            {
                WebHook webHookAdded = _webHooksManager.AddWebHook(webHook);
                webHooksAdded.Add(webHookAdded);
                Thread.Sleep(1);
            }

            return webHooksAdded;
        }

        private static void AssertWebHook(WebHook expectedWebHook, WebHook actualWebHook)
        {
            Assert.Equal(expectedWebHook.HookEventType, actualWebHook.HookEventType);
            Assert.Equal(expectedWebHook.HookAddress, actualWebHook.HookAddress);
            Assert.Equal(expectedWebHook.InsecureSsl, actualWebHook.InsecureSsl);
            Assert.True(!String.IsNullOrEmpty(actualWebHook.Id), "Received empty id for web hook");
        }

        private static Mock<IEnvironment> BuildEnvironmentMock()
        {
            var environmentMock = new Mock<IEnvironment>();
            environmentMock.Setup(e => e.DeploymentsPath)
                           .Returns("c:\\temp\\");
            return environmentMock;
        }

        private static Mock<IOperationLock> BuildHooksLockMock()
        {
            bool locked = false;

            var hooksLockMock = new Mock<IOperationLock>();

            hooksLockMock.SetupGet(l => l.IsHeld)
                         .Returns(() => locked);

            hooksLockMock.Setup(l => l.Lock())
                         .Returns(() => locked = true);

            hooksLockMock.Setup(l => l.Release())
                         .Callback(() => locked = false);

            return hooksLockMock;
        }

        private Mock<IFileSystem> BuildFileSystemMock()
        {
            var fileSystemMock = new Mock<IFileSystem>();
            var fileBaseMock = new Mock<FileBase>();

            fileSystemMock.SetupGet(fs => fs.File)
                      .Returns(fileBaseMock.Object);

            fileBaseMock.Setup(f => f.Exists("c:\\temp\\hooks"))
                        .Returns(true);

            fileBaseMock.Setup(f => f.ReadAllText("c:\\temp\\hooks"))
                        .Returns(() => _hooksFileContent);

            fileBaseMock.Setup(f => f.WriteAllText("c:\\temp\\hooks", It.IsAny<string>()))
                        .Callback<string, string>((path, contents) =>
                        {
                            _hooksFileContent = contents;
                        });

            return fileSystemMock;
        }

        private WebHooksManager BuildWebHooksManager()
        {
            var hooksLockMock = BuildHooksLockMock();
            var environmentMock = BuildEnvironmentMock();
            var fileSystemMock = BuildFileSystemMock();
            var tracer = Mock.Of<ITracer>();

            return new WebHooksManager(tracer, environmentMock.Object, hooksLockMock.Object, fileSystemMock.Object);
        }
    }
}