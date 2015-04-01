using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Net;
using System.Net.Http;
using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.Settings;
using Kudu.Contracts.SourceControl;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Core.Deployment;
using Kudu.Core.Infrastructure;
using Kudu.Core.SourceControl;
using Kudu.Core.SourceControl.Git;
using Kudu.Services.GitServer;
using Moq;
using Xunit;

namespace Kudu.Services.Test
{
    public class InfoRefsControllerFacts
    {
        [Fact]
        public void InfoRefsControllerDisallowsPushingAGitRepositoryWhenHgRepositoryIsAlreadyPresent()
        {
            // Arrange
            var settings = new Mock<IDeploymentSettingsManager>(MockBehavior.Strict);
            settings.Setup(s => s.GetValue("ScmType", false)).Returns("Git");

            var repositoryFactory = new Mock<IRepositoryFactory>(MockBehavior.Strict);
            var repository = new Mock<IRepository>();
            repository.SetupGet(r => r.RepositoryType).Returns(RepositoryType.Mercurial);
            repositoryFactory.Setup(s => s.GetRepository()).Returns(repository.Object);

            var request = new HttpRequestMessage();
            InfoRefsController controller = CreateController(settings: settings.Object, repositoryFactory: repositoryFactory.Object);
            controller.Request = request;

            // Act and Assert
            var response = controller.Execute("upload-pack");

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public void InfoRefsControllerInitialCommitBasic()
        {
            // Arrange
            var siteRoot = @"x:\vdir\site";
            var environment = GetEnvironment(siteRoot);
            var repositoryFactory = GetRepositoryFactory();
            var settings = GetDeploymentSettings();
            var fileSystem = GetFileSystem(siteRoot);
            var controller = CreateController(settings: settings,
                                              repositoryFactory: repositoryFactory,
                                              environment: environment,
                                              fileSystem: fileSystem,
                                              initLock: GetOperationLock());

            // Test
            controller.InitialCommitIfNecessary();

            // Assert
            Assert.NotNull(repositoryFactory.GetRepository());
            Assert.Equal(Constants.WebRoot, settings.GetValue(SettingsKeys.RepositoryPath));
            Assert.Equal(Path.Combine(siteRoot, Constants.WebRoot), environment.RepositoryPath);
        }

        [Fact]
        public void InfoRefsControllerInitialCommitRepositoryExists()
        {
            // Arrange
            var siteRoot = @"x:\vdir\site";
            var environment = GetEnvironment(siteRoot);
            var repositoryFactory = GetRepositoryFactory();
            var settings = GetDeploymentSettings();
            var fileSystem = GetFileSystem(siteRoot);
            var controller = CreateController(settings: settings,
                                              repositoryFactory: repositoryFactory,
                                              environment: environment,
                                              fileSystem: fileSystem,
                                              initLock: GetOperationLock());

            // Setup
            repositoryFactory.EnsureRepository(RepositoryType.Git);
            Assert.NotNull(repositoryFactory.GetRepository());

            // Test
            controller.InitialCommitIfNecessary();

            // Assert
            Assert.Null(settings.GetValue(SettingsKeys.RepositoryPath));
            Assert.Equal(Path.Combine(siteRoot, Constants.RepositoryPath), environment.RepositoryPath);
        }

        [Fact]
        public void InfoRefsControllerInitialCommitRepositorySettingExists()
        {
            // Arrange
            var siteRoot = @"x:\vdir\site";
            var environment = GetEnvironment(siteRoot);
            var repositoryFactory = GetRepositoryFactory();
            var settings = GetDeploymentSettings();
            var fileSystem = GetFileSystem(siteRoot);
            var controller = CreateController(settings: settings,
                                              repositoryFactory: repositoryFactory,
                                              environment: environment,
                                              fileSystem: fileSystem,
                                              initLock: GetOperationLock());

            // Setup
            settings.SetValue(SettingsKeys.RepositoryPath, Constants.WebRoot);

            // Test
            controller.InitialCommitIfNecessary();

            // Assert
            Assert.Null(repositoryFactory.GetRepository());
        }

        [Fact]
        public void InfoRefsControllerInitialCommitNoScmType()
        {
            // Arrange
            var siteRoot = @"x:\vdir\site";
            var environment = GetEnvironment(siteRoot);
            var repositoryFactory = GetRepositoryFactory();
            var settings = GetDeploymentSettings();
            var fileSystem = GetFileSystem(siteRoot);
            var controller = CreateController(settings: settings,
                                              repositoryFactory: repositoryFactory,
                                              environment: environment,
                                              fileSystem: fileSystem,
                                              initLock: GetOperationLock());

            // Setup
            settings.SetValue(SettingsKeys.ScmType, null);

            // Test
            controller.InitialCommitIfNecessary();

            // Assert
            Assert.Null(repositoryFactory.GetRepository());
            Assert.Null(settings.GetValue(SettingsKeys.RepositoryPath));
            Assert.Equal(Path.Combine(siteRoot, Constants.RepositoryPath), environment.RepositoryPath);
        }

        [Fact]
        public void InfoRefsControllerInitialCommitDefaultWebContent()
        {
            // Arrange
            var siteRoot = @"x:\vdir\site";
            var environment = GetEnvironment(siteRoot);
            var repositoryFactory = GetRepositoryFactory();
            var settings = GetDeploymentSettings();
            var fileSystem = GetFileSystem(siteRoot, isDefault: true);
            var controller = CreateController(settings: settings,
                                              repositoryFactory: repositoryFactory,
                                              environment: environment,
                                              fileSystem: fileSystem,
                                              initLock: GetOperationLock());

            // Test
            controller.InitialCommitIfNecessary();

            // Assert
            Assert.Null(repositoryFactory.GetRepository());
            Assert.Null(settings.GetValue(SettingsKeys.RepositoryPath));
            Assert.Equal(Path.Combine(siteRoot, Constants.RepositoryPath), environment.RepositoryPath);
        }

        [Fact]
        public void InfoRefsControllerInitialCommitWebRootRepositoryExists()
        {
            // Arrange
            var siteRoot = @"x:\vdir\site";
            var environment = GetEnvironment(siteRoot);
            var repositoryFactory = new Mock<IRepositoryFactory>(MockBehavior.Strict);
            var settings = GetDeploymentSettings();
            var fileSystem = GetFileSystem(siteRoot);
            var controller = CreateController(settings: settings,
                                              repositoryFactory: repositoryFactory.Object,
                                              environment: environment,
                                              fileSystem: fileSystem,
                                              initLock: GetOperationLock());

            // Setup
            repositoryFactory.Setup(s => s.GetRepository())
                .Returns(() => environment.RepositoryPath == Path.Combine(siteRoot, Constants.WebRoot) ? Mock.Of<IRepository>() : null);

            // Test
            controller.InitialCommitIfNecessary();

            // Assert
            Assert.Null(repositoryFactory.Object.GetRepository());
            Assert.Null(settings.GetValue(SettingsKeys.RepositoryPath));
            Assert.Equal(Path.Combine(siteRoot, Constants.RepositoryPath), environment.RepositoryPath);
        }

        [Theory]
        [MemberData("WebRootContents")]
        public void InfoRefsControllerIsDefaultWebRootContentTests(IWebRootContent scenario)
        {
            // Arrange
            var webroot = Path.Combine(@"x:\vdir\site", Constants.WebRoot);
            var fileSystem = new Mock<IFileSystem>();
            var fileBase = new Mock<FileBase>();
            var directoryBase = new Mock<DirectoryBase>();
            InfoRefsController controller = CreateController(fileSystem: fileSystem.Object);

            // Setup
            fileSystem.SetupGet(fs => fs.File)
                      .Returns(fileBase.Object);
            fileSystem.SetupGet(fs => fs.Directory)
                      .Returns(directoryBase.Object);
            scenario.Setup(fileBase, directoryBase, webroot);
            FileSystemHelpers.Instance = fileSystem.Object;

            // Act
            bool result = DeploymentHelper.IsDefaultWebRootContent(webroot);

            // Assert
            scenario.Verify(result);
        }

        public static IEnumerable<object[]> WebRootContents
        {
            get
            {
                yield return new object[] { new DefaultWebRootContent() };
                yield return new object[] { new MissingWebRootFolder() };
                yield return new object[] { new EmptyWebRootFolder() };
                yield return new object[] { new SingleWebRootContent() };
                yield return new object[] { new MultipleWebRootContent() };
            }
        }

        private InfoRefsController CreateController(IGitServer gitServer = null,
                                                    IDeploymentManager deploymentManager = null,
                                                    IDeploymentSettingsManager settings = null,
                                                    IRepositoryFactory repositoryFactory = null,
                                                    IEnvironment environment = null,
                                                    IFileSystem fileSystem = null,
                                                    IOperationLock initLock = null)
        {
            return new InfoRefsController(t =>
            {
                if (t == typeof(ITracer))
                {
                    return Mock.Of<ITracer>();
                }
                else if (t == typeof(IGitServer))
                {
                    return gitServer ?? Mock.Of<IGitServer>();
                }
                else if (t == typeof(IDeploymentSettingsManager))
                {
                    return settings ?? Mock.Of<IDeploymentSettingsManager>();
                }
                else if (t == typeof(IRepositoryFactory))
                {
                    return repositoryFactory ?? Mock.Of<IRepositoryFactory>();
                }
                else if (t == typeof(IEnvironment))
                {
                    return environment ?? Mock.Of<IEnvironment>();
                }
                else if (t == typeof(IFileSystem))
                {
                    return fileSystem ?? Mock.Of<IFileSystem>();
                }
                else if (t == typeof(IOperationLock))
                {
                    return initLock ?? Mock.Of<IOperationLock>();
                }

                throw new NotImplementedException("type " + t.Name + " is not implemented!");
            });
        }

        private IDeploymentSettingsManager GetDeploymentSettings()
        {
            var dict = new Dictionary<string, string>
            {
                { SettingsKeys.ScmType, "LocalGit" },
                { SettingsKeys.RepositoryPath, null }
            };

            var settings = new Mock<IDeploymentSettingsManager>(MockBehavior.Strict);
            settings.Setup(s => s.GetValue(SettingsKeys.ScmType, false))
                    .Returns(() => dict[SettingsKeys.ScmType]);
            settings.Setup(s => s.GetValue(SettingsKeys.RepositoryPath, false))
                    .Returns(() => dict[SettingsKeys.RepositoryPath]);
            settings.Setup(s => s.SetValue(It.IsAny<string>(), It.IsAny<string>()))
                    .Callback((string key, string value) => dict[key] = value);

            return settings.Object;
        }

        private IRepositoryFactory GetRepositoryFactory()
        {
            IRepository repository = null;
            var repositoryFactory = new Mock<IRepositoryFactory>(MockBehavior.Strict);
            repositoryFactory.Setup(s => s.GetRepository())
                             .Returns(() => repository);
            repositoryFactory.Setup(s => s.EnsureRepository(RepositoryType.Git))
                             .Returns(() => repository ?? (repository = Mock.Of<IRepository>()));
            return repositoryFactory.Object;
        }

        private IFileSystem GetFileSystem(string siteRoot, bool isDefault = false)
        {
            string webrootPath = Path.Combine(siteRoot, Constants.WebRoot);
            var fileSystem = new Mock<IFileSystem>();
            var fileBase = new Mock<FileBase>();
            var directoryBase = new Mock<DirectoryBase>();
            fileSystem.SetupGet(fs => fs.File)
                      .Returns(fileBase.Object);
            fileSystem.SetupGet(fs => fs.Directory)
                      .Returns(directoryBase.Object);
            directoryBase.Setup(d => d.Exists(webrootPath))
                         .Returns(true);
            if (isDefault)
            {
                string hostingstarthtml = Path.Combine(webrootPath, Constants.HostingStartHtml);
                directoryBase.Setup(d => d.GetFileSystemEntries(webrootPath))
                             .Returns(new[] { hostingstarthtml });
                fileBase.Setup(f => f.Exists(hostingstarthtml))
                        .Returns(true);
            }
            else
            {
                directoryBase.Setup(d => d.GetFileSystemEntries(webrootPath))
                             .Returns(new string[2]);
            }

            FileSystemHelpers.Instance = fileSystem.Object;

            return fileSystem.Object;
        }

        private IOperationLock GetOperationLock()
        {
            var locked = true;
            var initLock = new Mock<IOperationLock>();
            initLock.SetupGet(l => l.IsHeld)
                    .Returns(() => locked);
            initLock.Setup(l => l.Lock())
                    .Returns(() => locked = true);
            initLock.Setup(l => l.Release())
                    .Callback(() => locked = false);
            return initLock.Object;
        }

        private IEnvironment GetEnvironment(string siteRoot)
        {
            string webrootPath = Path.Combine(siteRoot, Constants.WebRoot);
            string repositoryPath = Path.Combine(siteRoot, "repository");
            var env = new Mock<IEnvironment>(MockBehavior.Strict);
            env.SetupGet(e => e.SiteRootPath)
               .Returns(siteRoot);
            env.SetupGet(e => e.WebRootPath)
               .Returns(webrootPath);
            env.SetupGet(e => e.RepositoryPath)
               .Returns(() => repositoryPath);
            env.SetupSet(e => e.RepositoryPath = It.IsAny<string>())
               .Callback((string value) => repositoryPath = value);
            return env.Object;
        }

        public interface IWebRootContent
        {
            void Setup(Mock<FileBase> fileBase, Mock<DirectoryBase> directoryBase, string webroot);

            void Verify(bool actual);
        }

        public class DefaultWebRootContent : IWebRootContent
        {
            public virtual void Setup(Mock<FileBase> fileBase, Mock<DirectoryBase> directoryBase, string webrootPath)
            {
                string hostingstarthtml = Path.Combine(webrootPath, Constants.HostingStartHtml);
                directoryBase.Setup(d => d.Exists(webrootPath))
                             .Returns(true);
                directoryBase.Setup(d => d.GetFileSystemEntries(webrootPath))
                             .Returns(new[] { hostingstarthtml });
                fileBase.Setup(f => f.Exists(hostingstarthtml))
                             .Returns(true);
            }

            public virtual void Verify(bool actual)
            {
                Assert.True(actual);
            }
        }

        public class MissingWebRootFolder : IWebRootContent
        {
            public virtual void Setup(Mock<FileBase> fileBase, Mock<DirectoryBase> directoryBase, string webroot)
            {
                directoryBase.Setup(d => d.Exists(webroot))
                             .Returns(false);
            }

            public virtual void Verify(bool actual)
            {
                Assert.True(actual);
            }
        }

        public class EmptyWebRootFolder : IWebRootContent
        {
            public virtual void Setup(Mock<FileBase> fileBase, Mock<DirectoryBase> directoryBase, string webroot)
            {
                directoryBase.Setup(d => d.Exists(webroot))
                             .Returns(true);
                directoryBase.Setup(d => d.GetFileSystemEntries(webroot))
                             .Returns(new string[0]);
            }

            public virtual void Verify(bool actual)
            {
                Assert.True(actual);
            }
        }

        public class SingleWebRootContent : IWebRootContent
        {
            public virtual void Setup(Mock<FileBase> fileBase, Mock<DirectoryBase> directoryBase, string webroot)
            {
                string indexhtml = Path.Combine(webroot, "index.html");
                directoryBase.Setup(d => d.Exists(webroot))
                             .Returns(true);
                directoryBase.Setup(d => d.GetFileSystemEntries(webroot))
                             .Returns(new[] { indexhtml });
                fileBase.Setup(f => f.Exists(indexhtml))
                             .Returns(true);
            }

            public virtual void Verify(bool actual)
            {
                Assert.False(actual);
            }
        }

        public class MultipleWebRootContent : IWebRootContent
        {
            public virtual void Setup(Mock<FileBase> fileBase, Mock<DirectoryBase> directoryBase, string webroot)
            {
                directoryBase.Setup(d => d.Exists(webroot))
                             .Returns(true);
                directoryBase.Setup(d => d.GetFileSystemEntries(webroot))
                             .Returns(new string[2]);
            }

            public virtual void Verify(bool actual)
            {
                Assert.False(actual);
            }
        }
    }
}