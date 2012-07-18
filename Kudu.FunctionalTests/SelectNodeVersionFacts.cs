using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using Kudu.Contracts.Tracing;
using Kudu.Core.Deployment;
using Xunit;

namespace Kudu.FunctionalTests
{
    public class SelectNodeVersionFacts
    {
        private string CreateTempDirectory()
        {
            string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(path);
            return path;
        }

        private class MockTracer : ITracer
        {
            public IDisposable Step(string message, IDictionary<string, string> attributes)
            {
                return null;
            }

            public void Trace(string message, IDictionary<string, string> attributes)
            {
            }
        }

        [Fact]
        public void TestFallbackToDefaultNodeJsVersionWithServerJsOnly()
        {
            var repoDir = this.CreateTempDirectory();
            var siteDir = this.CreateTempDirectory();
            var fileSystem = new FileSystem();
            fileSystem.File.WriteAllText(Path.Combine(repoDir, "server.js"), "some js");
            fileSystem.File.WriteAllText(Path.Combine(siteDir, "server.js"), "some js");
            var scriptDir = Path.Combine(System.Environment.CurrentDirectory, "scripts");

            var nodeSiteEnabler = new NodeSiteEnabler(fileSystem, repoDir, siteDir, scriptDir);
            Assert.True(nodeSiteEnabler.LooksLikeNode());
            string output = nodeSiteEnabler.SelectNodeVersion(new MockTracer());
            Assert.True(output.Contains("The package.json file is not present"),
                "the package.json was determined absent");
            Assert.True(!fileSystem.File.Exists(Path.Combine(siteDir, "iisnode.yml")),
                "iisnode.yml has not been created at " + siteDir);

            fileSystem.Directory.Delete(repoDir, true);
            fileSystem.Directory.Delete(siteDir, true);
        }

        [Fact]
        public void TestFallbackToDefaultNodeJsVersionWithServerJsAndEmptyPackageJson()
        {
            var repoDir = this.CreateTempDirectory();
            var siteDir = this.CreateTempDirectory();
            var fileSystem = new FileSystem();
            fileSystem.File.WriteAllText(Path.Combine(repoDir, "server.js"), "some js");
            fileSystem.File.WriteAllText(Path.Combine(siteDir, "server.js"), "some js");
            fileSystem.File.WriteAllText(Path.Combine(repoDir, "package.json"), "{}");
            fileSystem.File.WriteAllText(Path.Combine(siteDir, "package.json"), "{}");
            var scriptDir = Path.Combine(System.Environment.CurrentDirectory, "scripts");

            var nodeSiteEnabler = new NodeSiteEnabler(fileSystem, repoDir, siteDir, scriptDir);
            Assert.True(nodeSiteEnabler.LooksLikeNode());
            string output = nodeSiteEnabler.SelectNodeVersion(new MockTracer());
            Assert.True(output.Contains("The package.json file does not specify node.js engine version constraints"),
                "the package.json does not specify node.js version");
            Assert.True(!fileSystem.File.Exists(Path.Combine(siteDir, "iisnode.yml")),
                "iisnode.yml has not been created at " + siteDir);

            fileSystem.Directory.Delete(repoDir, true);
            fileSystem.Directory.Delete(siteDir, true);
        }

        [Fact]
        public void TestFallbackToDefaultNodeJsVersionWithIisnodeYmlLackingNodeProcessCommandLine()
        {
            var repoDir = this.CreateTempDirectory();
            var siteDir = this.CreateTempDirectory();
            var fileSystem = new FileSystem();
            fileSystem.File.WriteAllText(Path.Combine(repoDir, "server.js"), "some js");
            fileSystem.File.WriteAllText(Path.Combine(siteDir, "server.js"), "some js");
            fileSystem.File.WriteAllText(Path.Combine(repoDir, "iisnode.yml"), "foo: bar");
            fileSystem.File.WriteAllText(Path.Combine(siteDir, "iisnode.yml"), "foo: bar");
            var scriptDir = Path.Combine(System.Environment.CurrentDirectory, "scripts");

            var nodeSiteEnabler = new NodeSiteEnabler(fileSystem, repoDir, siteDir, scriptDir);
            Assert.True(nodeSiteEnabler.LooksLikeNode());
            string output = nodeSiteEnabler.SelectNodeVersion(new MockTracer());
            Assert.True(output.Contains("The package.json file is not present"),
                "the package.json does not specify node.js version");
            Assert.True(fileSystem.File.Exists(Path.Combine(siteDir, "iisnode.yml")),
                "iisnode.yml exists at " + siteDir);
            Assert.True(!fileSystem.File.ReadAllText(Path.Combine(siteDir, "iisnode.yml")).Contains("nodeProcessCommandLine"),
                "iisnode.yml at " + siteDir + " does not contain nodeProcessCommandLine");

            fileSystem.Directory.Delete(repoDir, true);
            fileSystem.Directory.Delete(siteDir, true);
        }

        [Fact]
        public void TestTurningAutomaticVersionSelectionOffWithIisnodeYmlWithNodeProcessCommandLine()
        {
            var repoDir = this.CreateTempDirectory();
            var siteDir = this.CreateTempDirectory();
            var fileSystem = new FileSystem();
            fileSystem.File.WriteAllText(Path.Combine(repoDir, "server.js"), "some js");
            fileSystem.File.WriteAllText(Path.Combine(siteDir, "server.js"), "some js");
            fileSystem.File.WriteAllText(Path.Combine(repoDir, "package.json"), "{ \"engines\": { \"node\": \"0.1.0\" }}");
            fileSystem.File.WriteAllText(Path.Combine(siteDir, "package.json"), "{ \"engines\": { \"node\": \"0.1.0\" }}");
            fileSystem.File.WriteAllText(Path.Combine(repoDir, "iisnode.yml"), "nodeProcessCommandLine: bar");
            fileSystem.File.WriteAllText(Path.Combine(siteDir, "iisnode.yml"), "nodeProcessCommandLine: bar");
            var scriptDir = Path.Combine(System.Environment.CurrentDirectory, "scripts");

            var nodeSiteEnabler = new NodeSiteEnabler(fileSystem, repoDir, siteDir, scriptDir);
            Assert.True(nodeSiteEnabler.LooksLikeNode());
            string output = nodeSiteEnabler.SelectNodeVersion(new MockTracer());
            Assert.True(output.Contains("The iisnode.yml file explicitly sets nodeProcessCommandLine"),
                "The iisnode.yml file explicitly sets nodeProcessCommandLine");
            Assert.True(fileSystem.File.Exists(Path.Combine(siteDir, "iisnode.yml")),
                "iisnode.yml exists at " + siteDir);
            Assert.True(fileSystem.File.ReadAllText(Path.Combine(siteDir, "iisnode.yml")).Contains("nodeProcessCommandLine: bar"),
                "iisnode.yml at " + siteDir + " remains unchanged");

            fileSystem.Directory.Delete(repoDir, true);
            fileSystem.Directory.Delete(siteDir, true);
        }

        [Fact]
        public void TestMismatchBetweenAvailableVersionsAndRequestedVersions()
        {
            var repoDir = this.CreateTempDirectory();
            var siteDir = this.CreateTempDirectory();
            var fileSystem = new FileSystem();
            fileSystem.File.WriteAllText(Path.Combine(repoDir, "server.js"), "some js");
            fileSystem.File.WriteAllText(Path.Combine(siteDir, "server.js"), "some js");
            fileSystem.File.WriteAllText(Path.Combine(repoDir, "package.json"), "{ \"engines\": { \"node\": \"0.1.0\" }}");
            fileSystem.File.WriteAllText(Path.Combine(siteDir, "package.json"), "{ \"engines\": { \"node\": \"0.1.0\" }}");
            var scriptDir = Path.Combine(System.Environment.CurrentDirectory, "scripts");

            var nodeSiteEnabler = new NodeSiteEnabler(fileSystem, repoDir, siteDir, scriptDir);
            Assert.True(nodeSiteEnabler.LooksLikeNode());
            try
            {
                nodeSiteEnabler.SelectNodeVersion(new MockTracer());
            }
            catch (InvalidOperationException e)
            {
                Assert.True(e.InnerException != null);
                Assert.True(e.InnerException.Message.Contains("No available node.js version matches"),
                    "No available node.js version matches");
            }

            Assert.True(!fileSystem.File.Exists(Path.Combine(siteDir, "iisnode.yml")),
                "iisnode.yml does not exist at " + siteDir);

            fileSystem.Directory.Delete(repoDir, true);
            fileSystem.Directory.Delete(siteDir, true);
        }

        [Fact]
        public void TestPositiveMatch()
        {
            var repoDir = this.CreateTempDirectory();
            var siteDir = this.CreateTempDirectory();
            var fileSystem = new FileSystem();
            fileSystem.File.WriteAllText(Path.Combine(repoDir, "server.js"), "some js");
            fileSystem.File.WriteAllText(Path.Combine(siteDir, "server.js"), "some js");
            fileSystem.File.WriteAllText(Path.Combine(repoDir, "package.json"), "{ \"engines\": { \"node\": \"> 0.6.0\" }}");
            fileSystem.File.WriteAllText(Path.Combine(siteDir, "package.json"), "{ \"engines\": { \"node\": \"> 0.6.0\" }}");
            var scriptDir = Path.Combine(System.Environment.CurrentDirectory, "scripts");

            var nodeSiteEnabler = new NodeSiteEnabler(fileSystem, repoDir, siteDir, scriptDir);
            Assert.True(nodeSiteEnabler.LooksLikeNode());
            string output = nodeSiteEnabler.SelectNodeVersion(new MockTracer());
            Assert.True(output.Contains("Selected node.js version"),
                "Selected node.js version");
            Assert.True(fileSystem.File.Exists(Path.Combine(siteDir, "iisnode.yml")),
                "iisnode.yml exists at " + siteDir);
            Assert.True(fileSystem.File.ReadAllText(Path.Combine(siteDir, "iisnode.yml")).Contains("nodeProcessCommandLine: "),
                 "iisnode.yml at " + siteDir + " contains nodeProcessCommandLine");

            fileSystem.Directory.Delete(repoDir, true);
            fileSystem.Directory.Delete(siteDir, true);
        }
    }
}
