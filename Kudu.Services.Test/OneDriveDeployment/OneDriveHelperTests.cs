using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq.Expressions;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Core.Deployment;
using Kudu.Core.Infrastructure;
using Kudu.Core.SourceControl;
using Kudu.Services.FetchHelpers;
using Kudu.TestHarness.Xunit;
using Moq;
using Xunit;

namespace Kudu.Services.Test.OneDriveDeployment
{
    [KuduXunitTestClass]
    public class OneDriveHelperSyncTests
    {
        private const string DefaultWebRoot = @"D:\home\site\wwwroot";

        [Fact]
        public async Task SyncBasicTests()
        {
            var mockTracer = new Mock<ITracer>();
            mockTracer
                .Setup(m => m.Trace(It.IsAny<string>(), It.IsAny<IDictionary<string, string>>()));

            var repository = Mock.Of<IRepository>();
            var fileSystem = new Mock<IFileSystem>();
            var fileBase = new Mock<FileBase>();
            var fileInfoFactory = new Mock<IFileInfoFactory>();
            var fileInfo = new Mock<FileInfoBase>();
            var dirBase = new Mock<DirectoryBase>();
            var dirInfoFactory = new Mock<IDirectoryInfoFactory>(); // mock dirInfo to make FileSystemHelpers.DeleteDirectorySafe not throw exception
            var dirInfoBase = new Mock<DirectoryInfoBase>();
            fileSystem.Setup(f => f.File).Returns(fileBase.Object);
            fileSystem.Setup(f => f.FileInfo).Returns(fileInfoFactory.Object);
            fileInfoFactory.Setup(f => f.FromFileName(It.IsAny<string>()))
                           .Returns(() => fileInfo.Object);
            fileSystem.Setup(f => f.Directory).Returns(dirBase.Object);
            fileSystem.Setup(f => f.DirectoryInfo).Returns(dirInfoFactory.Object);
            dirInfoFactory.Setup(d => d.FromDirectoryName(It.IsAny<string>())).Returns(dirInfoBase.Object);
            fileBase.Setup(fb => fb.Exists(It.IsAny<string>())).Returns((string path) =>
            {
                return (path != null && (path.EndsWith("f-delete") || path.EndsWith("bar.txt")));
            });
            fileBase.Setup(fb => fb.SetLastWriteTimeUtc(It.IsAny<string>(), It.IsAny<DateTime>()));

            dirBase.Setup(d => d.Exists(It.IsAny<string>())).Returns((string path) =>
            {
                return (path != null && (path.EndsWith("f-delete-dir") || path.EndsWith("f2")));
            });
            FileSystemHelpers.Instance = fileSystem.Object;

            // prepare change from OneDrive
            OneDriveModel.OneDriveChange change = new OneDriveModel.OneDriveChange();
            change.IsDeleted = false;

            // prepare OneDriveInfo
            var info = new OneDriveInfo();
            info.AccessToken = "fake-token";
            info.RepositoryUrl = "https://api.onedrive.com/v1.0/drive/special/approot:/fake-folder";
            info.TargetChangeset = new ChangeSet("id", "authorName", "authorEmail", "message", DateTime.UtcNow);

            // prepare http handler
            var handler = new TestMessageHandler((HttpRequestMessage message) =>
            {
                StringContent content = null;
                if (message != null && message.RequestUri != null)
                {
                    if (message.RequestUri.AbsoluteUri.Equals(info.RepositoryUrl))
                    {
                        content = new StringContent(@"{ 'id': 'fake-id'}", Encoding.UTF8, "application/json");
                        return new HttpResponseMessage { Content = content };
                    }
                    else if (message.RequestUri.AbsoluteUri.Equals("https://api.onedrive.com/v1.0/drive/items/fake-id/view.changes"))
                    {
                        content = new StringContent(ViewChangePayload, Encoding.UTF8, "application/json");
                        return new HttpResponseMessage { Content = content };
                    }
                    else if (message.RequestUri.AbsoluteUri.EndsWith("items/A6034FFBC93398FD!331")
                        || message.RequestUri.AbsoluteUri.EndsWith("items/A6034FFBC93398FD!330"))
                    {
                        content = new StringContent(@"{ '@content.downloadUrl': 'http://site-does-not-exist.microsoft.com'}", Encoding.UTF8, "application/json");
                        return new HttpResponseMessage { Content = content };
                    }
                }

                content = new StringContent("test file content", Encoding.UTF8, "application/json");
                return new HttpResponseMessage { Content = content };
            });

            // perform action
            OneDriveHelper helper = CreateMockOneDriveHelper(handler: handler, tracer: mockTracer.Object);
            await helper.Sync(info, repository);

            // verification
            /*
             Sycing f2 to wwwroot:
             
                 There are 6 changes
                    2 deletion
                      f2\f-delete       (existed as file)
                      f2\f-delete-dir   (existed as folder)
  
                    2 file changes
                      f2\foo.txt        (not existed)
                      f2\f22\bar.txt    (existed)

                    2 folder chagnes
                      f2                (existed)
                      f2\f22            (not existed)
             */

            // deletion
            mockTracer.Verify(t => t.Trace(@"Deleted file D:\home\site\wwwroot\f-delete", It.Is<IDictionary<string, string>>(d => d.Count == 0)));
            mockTracer.Verify(t => t.Trace(@"Deleted directory D:\home\site\wwwroot\f-delete-dir", It.Is<IDictionary<string, string>>(d => d.Count == 0)));

            // file changes
            mockTracer.Verify(t => t.Trace(@"Creating file D:\home\site\wwwroot\foo.txt ...", It.Is<IDictionary<string, string>>(d => d.Count == 0)));
            mockTracer.Verify(t => t.Trace(@"Updating file D:\home\site\wwwroot\f22\bar.txt ...", It.Is<IDictionary<string, string>>(d => d.Count == 0)));

            mockTracer.Verify(t => t.Trace(@"Deleted file D:\home\site\wwwroot\f-delete", It.Is<IDictionary<string, string>>(d => d.Count == 0)));
            mockTracer.Verify(t => t.Trace(@"Deleted directory D:\home\site\wwwroot\f-delete-dir", It.Is<IDictionary<string, string>>(d => d.Count == 0)));

            // directory changes
            mockTracer.Verify(t => t.Trace(@"Ignore folder f2", It.Is<IDictionary<string, string>>(d => d.Count == 0)));
            mockTracer.Verify(t => t.Trace(@"Creating directory D:\home\site\wwwroot\f22 ...", It.Is<IDictionary<string, string>>(d => d.Count == 0)));
        }

        private static OneDriveHelper CreateMockOneDriveHelper(TestMessageHandler handler, ITracer tracer, IDeploymentSettingsManager settings = null, IEnvironment env = null)
        {
            var envMock = new Mock<IEnvironment>();
            envMock.Setup(e => e.WebRootPath).Returns(DefaultWebRoot);
            var helper = new Mock<OneDriveHelper>(tracer, Mock.Of<IDeploymentStatusManager>(), settings ?? Mock.Of<IDeploymentSettingsManager>(), env ?? envMock.Object);

            helper.CallBase = true;
            helper.Object.Logger = Mock.Of<ILogger>();

            helper.Setup(h => h.CreateHttpClient(It.IsAny<string>()))
                .Returns(() =>
                {
                    handler.Disposed = false;
                    return new HttpClient(handler) { BaseAddress = new Uri("http://site-does-not-exist.microsoft.com") };
                });

            helper.Setup(h => h.WriteToFile(It.IsAny<Stream>(), It.IsAny<string>())).Returns(() =>
            {
                return Task.Delay(TimeSpan.FromMilliseconds(5));
            });

            return helper.Object;
        }

        #region Inline mock data for OneDrive view.change request
        private const string ViewChangePayload =
@"{
    ""@changes.token"": ""aTE09NjM1Njk1ODgzMzU4OTM7SUQ9QTYwMzRGRkJDOTMzOThGRCEzMjg7TFI9NjM1Njk1ODgzNjM1MTc7RVA9NDtTTz0y"",
    ""@changes.hasMoreChanges"": false,
    ""value"": [
        {
            ""createdBy"": {
                ""application"": {
                    ""displayName"": ""OneDrive website"",
                    ""id"": ""44048800""
                },
                ""user"": {
                    ""displayName"": ""Xiaomin Wu"",
                    ""id"": ""a6034ffbc93398fd""
                }
            },
            ""createdDateTime"": ""2015-06-11T02:57:57.193Z"",
            ""cTag"": ""adDpBNjAzNEZGQkM5MzM5OEZEITMyOC42MzU2OTU4ODMzNTg5MzAwMDA"",
            ""eTag"": ""aQTYwMzRGRkJDOTMzOThGRCEzMjguMA"",
            ""id"": ""A6034FFBC93398FD!328"",
            ""lastModifiedBy"": {
                ""application"": {
                    ""displayName"": ""OneDrive website"",
                    ""id"": ""44048800""
                },
                ""user"": {
                    ""displayName"": ""Xiaomin Wu"",
                    ""id"": ""a6034ffbc93398fd""
                }
            },
            ""lastModifiedDateTime"": ""2015-06-11T02:58:55.893Z"",
            ""name"": ""f2"",
            ""parentReference"": {
                ""driveId"": ""a6034ffbc93398fd"",
                ""id"": ""A6034FFBC93398FD!113""
            },
            ""size"": 6,
            ""webUrl"": ""https://onedrive.live.com/redir?resid=A6034FFBC93398FD!328"",
            ""fileSystemInfo"": {
                ""createdDateTime"": ""2015-06-11T02:57:57.193Z"",
                ""lastModifiedDateTime"": ""2015-06-11T02:57:57.193Z""
            },
            ""folder"": {
                ""childCount"": 0
            }
        },
        {
            ""createdBy"": {
                ""application"": {
                    ""displayName"": ""OneDrive website"",
                    ""id"": ""44048800""
                },
                ""user"": {
                    ""displayName"": ""Xiaomin Wu"",
                    ""id"": ""a6034ffbc93398fd""
                }
            },
            ""createdDateTime"": ""2015-06-11T02:58:05.877Z"",
            ""cTag"": ""adDpBNjAzNEZGQkM5MzM5OEZEITMyOS42MzU2OTU4ODMyNzUyNzAwMDA"",
            ""eTag"": ""aQTYwMzRGRkJDOTMzOThGRCEzMjkuMA"",
            ""id"": ""A6034FFBC93398FD!329"",
            ""lastModifiedBy"": {
                ""application"": {
                    ""displayName"": ""OneDrive website"",
                    ""id"": ""44048800""
                },
                ""user"": {
                    ""displayName"": ""Xiaomin Wu"",
                    ""id"": ""a6034ffbc93398fd""
                }
            },
            ""lastModifiedDateTime"": ""2015-06-11T02:58:47.527Z"",
            ""name"": ""f22"",
            ""parentReference"": {
                ""driveId"": ""a6034ffbc93398fd"",
                ""id"": ""A6034FFBC93398FD!328""
            },
            ""size"": 3,
            ""webUrl"": ""https://onedrive.live.com/redir?resid=A6034FFBC93398FD!329"",
            ""fileSystemInfo"": {
                ""createdDateTime"": ""2015-06-11T02:58:05.877Z"",
                ""lastModifiedDateTime"": ""2015-06-11T02:58:05.877Z""
            },
            ""folder"": {
                ""childCount"": 0
            }
        },
        {
            ""@content.downloadUrl"": ""https://public-dm2305.files.1drv.com/y3mpyu5LUrZOdf69VW0aQA4_zscCruoAc_QLrX4dAP11dilQ9vEIWc47fIMCqMr5H_bqw8kMKeDzvZmxshTIJGnN_1p3_UJA4mohszh_0UBU1u1QfPOIh8KNYhaUEme17_PXRG4hxc1TAwkLM4-a5ZPWQybTlNJbcMWlwqqU5zI8Ie8Iqz4OnSRcGsR-UNkSGzB"",
            ""createdBy"": {
                ""user"": {
                    ""displayName"": ""Xiaomin Wu"",
                    ""id"": ""a6034ffbc93398fd""
                }
            },
            ""createdDateTime"": ""2015-06-11T02:58:12.88Z"",
            ""cTag"": ""aYzpBNjAzNEZGQkM5MzM5OEZEITMzMC4yNTg"",
            ""eTag"": ""aQTYwMzRGRkJDOTMzOThGRCEzMzAuMw"",
            ""id"": ""A6034FFBC93398FD!330"",
            ""lastModifiedBy"": {
                ""application"": {
                    ""displayName"": ""OneDrive website"",
                    ""id"": ""44048800""
                },
                ""user"": {
                    ""displayName"": ""Xiaomin Wu"",
                    ""id"": ""a6034ffbc93398fd""
                }
            },
            ""lastModifiedDateTime"": ""2015-06-11T02:58:55.893Z"",
            ""name"": ""foo.txt"",
            ""parentReference"": {
                ""driveId"": ""a6034ffbc93398fd"",
                ""id"": ""A6034FFBC93398FD!328""
            },
            ""size"": 3,
            ""webUrl"": ""https://onedrive.live.com/redir?resid=A6034FFBC93398FD!330"",
            ""file"": {
                ""hashes"": {
                    ""crc32Hash"": ""2165738C"",
                    ""sha1Hash"": ""0BEEC7B5EA3F0FDBC95D0DD47F3C5BC275DA8A33""
                },
                ""mimeType"": ""text/plain""
            },
            ""fileSystemInfo"": {
                ""createdDateTime"": ""2015-06-11T02:58:12.88Z"",
                ""lastModifiedDateTime"": ""2015-06-11T02:58:55.893Z""
            }
        },
        {
            ""@content.downloadUrl"": ""https://public-dm2305.files.1drv.com/y3m4khG3nKfb9osC59cCqtCpfqNopq0VrM9UnOevWJb2HKYL1csIOD_ZuzRSGlaPCctRiaBkdbBByemYizkOjH6hYUqHgqu_EaHXVGtGvILp8Wityh30_rl_cz1oXIf6s-doRBA4pcUCvGDWhvsRtb9f10BONkOqJ6Q7aB6USSs6DSkL6jLULCzAFDzVIMVw8XU"",
            ""createdBy"": {
                ""user"": {
                    ""displayName"": ""Xiaomin Wu"",
                    ""id"": ""a6034ffbc93398fd""
                }
            },
            ""createdDateTime"": ""2015-06-11T02:58:34.583Z"",
            ""cTag"": ""aYzpBNjAzNEZGQkM5MzM5OEZEITMzMS4yNTg"",
            ""eTag"": ""aQTYwMzRGRkJDOTMzOThGRCEzMzEuMw"",
            ""id"": ""A6034FFBC93398FD!331"",
            ""lastModifiedBy"": {
                ""application"": {
                    ""displayName"": ""OneDrive website"",
                    ""id"": ""44048800""
                },
                ""user"": {
                    ""displayName"": ""Xiaomin Wu"",
                    ""id"": ""a6034ffbc93398fd""
                }
            },
            ""lastModifiedDateTime"": ""2015-06-11T02:58:47.527Z"",
            ""name"": ""bar.txt"",
            ""parentReference"": {
                ""driveId"": ""a6034ffbc93398fd"",
                ""id"": ""A6034FFBC93398FD!329""
            },
            ""size"": 3,
            ""webUrl"": ""https://onedrive.live.com/redir?resid=A6034FFBC93398FD!331"",
            ""file"": {
                ""hashes"": {
                    ""crc32Hash"": ""AA8CFF76"",
                    ""sha1Hash"": ""62CDB7020FF920E5AA642C3D4066950DD1F01F4D""
                },
                ""mimeType"": ""text/plain""
            },
            ""fileSystemInfo"": {
                ""createdDateTime"": ""2015-06-11T02:58:34.583Z"",
                ""lastModifiedDateTime"": ""2015-06-11T02:58:47.527Z""
            }
        },
        {
            ""cTag"": ""adDpBNjAzNEZGQkM5MzM5OEZEITMzMi42MzU2OTU4ODUzMzAyMDAwMDA"",
            ""eTag"": ""aQTYwMzRGRkJDOTMzOThGRCEzMzIuMQ"",
            ""id"": ""A6034FFBC93398FD!332"",
            ""lastModifiedBy"": {
                ""user"": {
                    ""displayName"": ""Xiaomin Wu"",
                    ""id"": ""a6034ffbc93398fd""
                }
            },
            ""lastModifiedDateTime"": ""2015-06-11T03:02:13.02Z"",
            ""name"": ""f-delete"",
            ""parentReference"": {
                ""driveId"": ""a6034ffbc93398fd"",
                ""id"": ""A6034FFBC93398FD!328""
            },
            ""webUrl"": ""https://onedrive.live.com/redir?resid=A6034FFBC93398FD!332"",
            ""deleted"": {

            },
            ""folder"": {
                ""childCount"": 0
            }
        },
        {
            ""cTag"": ""adDpBNjAzNEZGQkM5MzM5OEZEITMzMi42MzU2OTU4ODUzMzAyMDAwMDB"",
            ""eTag"": ""aQTYwMzRGRkJDOTMzOThGRCEzMzIuMR"",
            ""id"": ""A6034FFBC93398FD!333"",
            ""lastModifiedBy"": {
                ""user"": {
                    ""displayName"": ""Xiaomin Wu"",
                    ""id"": ""a6034ffbc93398fd""
                }
            },
            ""lastModifiedDateTime"": ""2015-06-11T03:02:13.02Z"",
            ""name"": ""f-delete-dir"",
            ""parentReference"": {
                ""driveId"": ""a6034ffbc93398fd"",
                ""id"": ""A6034FFBC93398FD!328""
            },
            ""webUrl"": ""https://onedrive.live.com/redir?resid=A6034FFBC93398FD!332"",
            ""deleted"": {

            }
        }
    ],
    ""@odata.nextLink"": ""https://api.onedrive.com/v1.0/drives('me')/items('A6034FFBC93398FD!328')/view.changes?token=aTE09NjM1Njk1ODgzMzU4OTM7SUQ9QTYwMzRGRkJDOTMzOThGRCEzMjg7TFI9NjM1Njk1ODgzNjM1MTc7RVA9NDtTTz0y""
}";
        #endregion
    }
}
