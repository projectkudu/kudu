using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.Tracing;
using Kudu.Core.Hooks;
using Moq;
using Xunit;

namespace Kudu.Core.Test.Deployment
{
    public class WebHooksManagerTests
    {
        private string[] _hooksFileContent = new string[] { };
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
                new WebHook(HookEventType.PostDeployment, "http://www.gothere.com/aaabbbbbbaaa")
            };

            WebHook[] expectedWebHooks = inputWebHooks;

            AddWebHooks(inputWebHooks);

            AssertWebHooks(expectedWebHooks);
        }

        [Fact]
        public void RemoveWebHookShouldSucceed()
        {
            WebHook[] inputWebHooks = new WebHook[]
            {
                new WebHook(HookEventType.PostDeployment, "http://www.gothere.com/aaabbbbbbaaa"),
                new WebHook(HookEventType.PostDeployment, "http://www.gothere.com/aaabbbbbbaaa2"),
                new WebHook(HookEventType.PostDeployment, "http://www.gothere.com/aaabbbbbbaaa3")
            };

            AddWebHooks(inputWebHooks);

            _webHooksManager.RemoveWebHook("http://www.gothere.com/aaabbbbbbaaa2");
            WebHook[] expectedWebHooks = new WebHook[]
            {
                new WebHook(HookEventType.PostDeployment, "http://www.gothere.com/aaabbbbbbaaa"),
                new WebHook(HookEventType.PostDeployment, "http://www.gothere.com/aaabbbbbbaaa3"),
            };
            AssertWebHooks(expectedWebHooks);

            _webHooksManager.RemoveWebHook("http://www.gothere.com/aaabbbbbbaaa");
            expectedWebHooks = new WebHook[]
            {
                new WebHook(HookEventType.PostDeployment, "http://www.gothere.com/aaabbbbbbaaa3")
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
                new WebHook(HookEventType.PostDeployment, "http://www.gothere.com/aaabbbbbbaaa"),
                new WebHook(HookEventType.PostDeployment, "http://www.gothere.com/aaabbbbbbaaa2"),
                new WebHook(HookEventType.PostDeployment, "http://www.gothere.com/aaabbbbbbaaa3")
            };

            WebHook[] expectedWebHooks = inputWebHooks;

            AddWebHooks(inputWebHooks);

            AssertWebHooks(expectedWebHooks);
        }

        [Fact]
        public void AddSameAddressWebHookShouldAddOnlySingleTime()
        {
            WebHook[] inputWebHooks = new WebHook[]
            {
                new WebHook(HookEventType.PostDeployment, "http://www.gothere.com/aaabbbbbbaaa"),
                new WebHook(HookEventType.PostDeployment, "http://www.gothere.com/aaabbbbbbaaa"),
                new WebHook(HookEventType.PostDeployment, "http://www.gothere.com/aaabbbbbbaaa2"),
                new WebHook(HookEventType.PostDeployment, "http://www.gothere.com/aaabbbbbbaaa2"),
                new WebHook(HookEventType.PostDeployment, "http://www.gothere.com/aaabbbbbbaaa")
            };

            WebHook[] expectedWebHooks = new WebHook[]
            {
                new WebHook(HookEventType.PostDeployment, "http://www.gothere.com/aaabbbbbbaaa"),
                new WebHook(HookEventType.PostDeployment, "http://www.gothere.com/aaabbbbbbaaa2"),
            };

            AddWebHooks(inputWebHooks);

            AssertWebHooks(expectedWebHooks);
        }

        [Fact]
        public void AddWebHookWithInvalidAddressShouldThrowFormatException()
        {
            Assert.Throws<FormatException>(() =>
                _webHooksManager.AddWebHook(new WebHook(HookEventType.PostDeployment, "htsp://\\invalidurl")));
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

        private void AddWebHooks(WebHook[] webHooks)
        {
            foreach (var webHook in webHooks)
            {
                _webHooksManager.AddWebHook(webHook);
            }
        }

        private static void AssertWebHook(WebHook expectedWebHook, WebHook actualWebHook)
        {
            Assert.Equal(expectedWebHook.HookEventType, actualWebHook.HookEventType);
            Assert.Equal(expectedWebHook.HookAddress, actualWebHook.HookAddress);
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

            fileBaseMock.Setup(f => f.ReadAllLines("c:\\temp\\hooks"))
                        .Returns(() => _hooksFileContent);

            fileBaseMock.Setup(f => f.WriteAllText("c:\\temp\\hooks", It.IsAny<string>()))
                        .Callback<string, string>((path, contents) =>
                        {
                            _hooksFileContent = contents.Split('\n');
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