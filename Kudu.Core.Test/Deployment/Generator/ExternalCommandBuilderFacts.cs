using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using Kudu.Contracts.Settings;
using Kudu.Core.Deployment;
using Kudu.Core.Deployment.Generator;
using Kudu.Core.Infrastructure;
using Kudu.TestHarness;
using Moq;
using Xunit;

namespace Kudu.Core.Test.Deployment.Generator
{
    public class ExternalCommandBuilderFacts
    {
        [Fact]
        public void GetPostBuildActionScriptsShouldReturnDefaultFiles()
        {
            const string DeploymentToolsPath = @"x:\DeploymentToolsPath";
            string[] DefaultActionScripts = new string[] {
                Path.Combine(DeploymentToolsPath, "CmdTest.cmd"),
                Path.Combine(DeploymentToolsPath, "BatTest.bat"),
                Path.Combine(DeploymentToolsPath, "Ps1Test.ps1")
            };

            var environmentMock = new Mock<IEnvironment>();
            environmentMock.Setup(e => e.RepositoryPath).Returns(@"e:\");
            environmentMock.Setup(e => e.DeploymentToolsPath).Returns(DeploymentToolsPath);

            var deploymentSettingsMock = new Mock<IDeploymentSettingsManager>();

            var directoryMock = new Mock<DirectoryBase>();
            directoryMock.Setup(d => d.Exists(Path.Combine(DeploymentToolsPath, "PostDeploymentActions"))).Returns(true);
            directoryMock.Setup(d => d.GetFiles(
                Path.Combine(DeploymentToolsPath, "PostDeploymentActions"),
                "*")
            ).Returns(DefaultActionScripts.Union(new string[] { Path.Combine(DeploymentToolsPath, "PostDeploymentActions", "Foo.txt") }).ToArray());

            var fileSystemMock = new Mock<IFileSystem>();
            fileSystemMock.Setup(f => f.Directory).Returns(directoryMock.Object);
            FileSystemHelpers.Instance = fileSystemMock.Object;

            var builder = new CustomBuilder(
                environmentMock.Object,
                deploymentSettingsMock.Object,
                Mock.Of<IBuildPropertyProvider>(),
                string.Empty,
                string.Empty);

            TestTracer.Trace("Should return action script from default folder");
            IList<string> actionScripts = builder.GetPostBuildActionScripts();
            Assert.NotEmpty(actionScripts);
            Assert.Equal(DefaultActionScripts.Count(), actionScripts.Count);
            Assert.NotEqual(DefaultActionScripts[0], actionScripts[0]);
            Array.Sort(DefaultActionScripts);
            Assert.Equal(DefaultActionScripts[0], actionScripts[0]);
            Assert.Equal(DefaultActionScripts[1], actionScripts[1]);
            Assert.Equal(DefaultActionScripts[2], actionScripts[2]);

            const string CustomDeploymentActionDir = @"x:\CustomDeploymentToolsPath\CustomPostDeploymentActions";
            string[] CustomActionScripts = new string[] {
                Path.Combine(DeploymentToolsPath, "CmdTest-Custom.cmd"),
                Path.Combine(DeploymentToolsPath, "BatTest-Custom.bat"),
                Path.Combine(DeploymentToolsPath, "Ps1Test-Custom.ps1")
            };

            directoryMock.Setup(d => d.Exists(CustomDeploymentActionDir)).Returns(true);
            directoryMock.Setup(d => d.GetFiles(CustomDeploymentActionDir, "*"))
                         .Returns(CustomActionScripts.Union(new string[] { Path.Combine(CustomDeploymentActionDir, "Foo.txt") }).ToArray());

            TestTracer.Trace("Override SCM_POST_DEPLOYMENT_ACTIONS_PATH to a custom location");
            deploymentSettingsMock.Setup(d => 
                d.GetValue(It.Is<string>(key => "SCM_POST_DEPLOYMENT_ACTIONS_PATH".Equals(key)), It.IsAny<bool>()))
            .Returns(CustomDeploymentActionDir);

            TestTracer.Trace("Should return action script from custom folder");
            actionScripts = builder.GetPostBuildActionScripts();
            Assert.NotEmpty(actionScripts);
            Assert.Equal(DefaultActionScripts.Count(), actionScripts.Count);
            Assert.NotEqual(DefaultActionScripts[0], actionScripts[0]);
            Assert.NotEqual(DefaultActionScripts[1], actionScripts[1]);
            Assert.NotEqual(DefaultActionScripts[2], actionScripts[2]);

            Assert.Equal(CustomActionScripts.Count(), actionScripts.Count);
            Assert.NotEqual(CustomActionScripts[0], actionScripts[0]);
            Array.Sort(CustomActionScripts);

            Assert.Equal(CustomActionScripts[0], actionScripts[0]);
            Assert.Equal(CustomActionScripts[1], actionScripts[1]);
            Assert.Equal(CustomActionScripts[2], actionScripts[2]);
        }

        [Fact]
        public void GetPostBuildActionScriptsShouldReturnActionScriptFromSiteExtensions()
        {
            const string DeploymentToolsPath = @"x:\DeploymentToolsPath";
            string[] DefaultActionScripts = new string[] {
                Path.Combine(DeploymentToolsPath, "CmdTest.cmd"),
                Path.Combine(DeploymentToolsPath, "BatTest.bat"),
                Path.Combine(DeploymentToolsPath, "Ps1Test.ps1")
            };

            var environmentMock = new Mock<IEnvironment>();
            environmentMock.Setup(e => e.RepositoryPath).Returns(@"e:\");
            environmentMock.Setup(e => e.DeploymentToolsPath).Returns(DeploymentToolsPath);
            
            var directoryMock = new Mock<DirectoryBase>();
            directoryMock.Setup(d => d.Exists(Path.Combine(DeploymentToolsPath, "PostDeploymentActions"))).Returns(true);
            directoryMock.Setup(d => d.GetFiles(
                Path.Combine(DeploymentToolsPath, "PostDeploymentActions"),
                "*")
            ).Returns(DefaultActionScripts.Union(new string[] { Path.Combine(DeploymentToolsPath, "PostDeploymentActions", "Foo.txt") }).ToArray());

            System.Environment.SetEnvironmentVariable("Foo_EXTENSION_VERSION", "1.0.0", EnvironmentVariableTarget.Process);
            System.Environment.SetEnvironmentVariable("Bar_EXTENSION_VERSION", "latest", EnvironmentVariableTarget.Process);

            try
            {
                string siteExtensionFolder = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86), "SiteExtensions");

                string[] FooActionScripts = new string[] {
                    Path.Combine(siteExtensionFolder, "Foo", "1.0.0", "PostDeploymentActions", "CmdTest-Foo.cmd"),
                    Path.Combine(siteExtensionFolder, "Foo", "1.0.0","PostDeploymentActions", "BatTest-Foo.bat"),
                    Path.Combine(siteExtensionFolder, "Foo", "1.0.0", "PostDeploymentActions","Ps1Test-Foo.ps1")
                };

                string[] BarActionScripts = new string[] {
                    Path.Combine(siteExtensionFolder, "Bar", "10.0.0","PostDeploymentActions", "CmdTest-Bar.cmd"),
                    Path.Combine(siteExtensionFolder, "Bar", "10.0.0","PostDeploymentActions", "BatTest-Bar.bat"),
                    Path.Combine(siteExtensionFolder, "Bar", "10.0.0","PostDeploymentActions", "Ps1Test-Bar.ps1")
                };

                directoryMock.Setup(d => d.Exists(Path.Combine(siteExtensionFolder, "Foo"))).Returns(true);
                directoryMock.Setup(d => d.Exists(Path.Combine(siteExtensionFolder, "Foo", "1.0.0", "PostDeploymentActions"))).Returns(true);
                directoryMock.Setup(d => d.GetFiles(
                    Path.Combine(siteExtensionFolder, "Foo", "1.0.0", "PostDeploymentActions"),
                    "*")
                ).Returns(FooActionScripts.Union(new string[] { Path.Combine(siteExtensionFolder, "Foo", "1.0.0", "PostDeploymentActions", "Foo.txt") }).ToArray());

                directoryMock.Setup(d => d.Exists(Path.Combine(siteExtensionFolder, "Bar"))).Returns(true);
                directoryMock.Setup(d => d.Exists(Path.Combine(siteExtensionFolder, "Bar", "10.0.0", "PostDeploymentActions"))).Returns(true);
                directoryMock.Setup(d => d.GetFiles(
                    Path.Combine(siteExtensionFolder, "Bar", "10.0.0", "PostDeploymentActions"),
                    "*")
                ).Returns(BarActionScripts.Union(new string[] { Path.Combine(siteExtensionFolder, "Bar", "10.0.0", "PostDeploymentActions", "Foo.txt") }).ToArray());

                directoryMock.Setup(d => d.GetDirectories(Path.Combine(siteExtensionFolder, "Bar"))
                ).Returns(new string[] {
                Path.Combine(siteExtensionFolder, "Bar", "10.0.0"),
                Path.Combine(siteExtensionFolder, "Bar", "1.0.0"),
                Path.Combine(siteExtensionFolder, "Bar", "2.0.0"),
                });

                var fileSystemMock = new Mock<IFileSystem>();
                fileSystemMock.Setup(f => f.Directory).Returns(directoryMock.Object);
                FileSystemHelpers.Instance = fileSystemMock.Object;

                var builder = new CustomBuilder(
                    environmentMock.Object,
                    Mock.Of<IDeploymentSettingsManager>(),
                    Mock.Of<IBuildPropertyProvider>(),
                    string.Empty,
                    string.Empty);

                IList<string> actionScripts = builder.GetPostBuildActionScripts();
                Assert.Equal(DefaultActionScripts.Count() + FooActionScripts.Count() + BarActionScripts.Count(), actionScripts.Count);
            }
            finally
            {
                System.Environment.SetEnvironmentVariable("Foo_EXTENSION_VERSION", null, EnvironmentVariableTarget.Process);
                System.Environment.SetEnvironmentVariable("Bar_EXTENSION_VERSION", null, EnvironmentVariableTarget.Process);
            }
        }
    }
}
