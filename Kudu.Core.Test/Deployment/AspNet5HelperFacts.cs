using Kudu.Core.Infrastructure;
using Kudu.Core.SourceControl;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using System.IO;

namespace Kudu.Core.Test.Deployment
{
    public class AspNet5HelperFacts
    {
        [Fact]
        public void GetSdkObjectWhenGlobalJsondDoesntHaveOne()
        {
            //Arrange
            var sdkObject = new
            {
                project = new[] { "src", "test" }
            };
            FileSystemHelpers.Instance = MockFileSystem.GetMockFileSystem(@"x:\repo\global.json", () => JsonConvert.SerializeObject(sdkObject));
            var arch = AspNet5Helper.GetDefaultAspNet5RuntimeArchitecture();
            var mockFileFinder = new MockFileFinder();
            //Act
            var sdk = AspNet5Helper.GetAspNet5Sdk(@"x:\repo", mockFileFinder);

            //Assert
            Assert.NotNull(sdk);
            Assert.Equal(Constants.DnxDefaultVersion, sdk.Version);
            Assert.Equal(Constants.DnxDefaultClr, sdk.Runtime);
            Assert.Equal(arch, sdk.Architecture);
        }

        [Fact]
        public void GetSdkObjectFromGlobalJson()
        {
            //Arrange
            var sdkObject = new
            {
                project = new[] { "src", "test" },
                sdk = new
                {
                    version = "1.0.0",
                    runtime = "coreclr",
                    architecture = "arm"
                }
            };
            FileSystemHelpers.Instance = MockFileSystem.GetMockFileSystem(@"x:\repo\global.json", () => JsonConvert.SerializeObject(sdkObject));
            var arch = AspNet5Helper.GetDefaultAspNet5RuntimeArchitecture();
            var mockFileFinder = new MockFileFinder();

            //Act
            var sdk = AspNet5Helper.GetAspNet5Sdk(@"x:\repo", mockFileFinder);

            //Assert
            Assert.NotNull(sdk);
            Assert.Equal(sdkObject.sdk.version, sdk.Version);
            Assert.Equal(sdkObject.sdk.runtime, sdk.Runtime);
            Assert.Equal(sdkObject.sdk.architecture, sdk.Architecture);
        }

    }

    internal class MockFileFinder : IFileFinder
    {
        public IEnumerable<string> ListFiles(string path, SearchOption searchOption, params string[] lookupList)
        {
            return lookupList.Select(i => FileSystemHelpers.GetFiles(path, i, searchOption)).SelectMany(i => i);
        }
    }
}
