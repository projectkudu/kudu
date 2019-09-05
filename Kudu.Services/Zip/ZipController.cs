using System;
using System.IO;
using System.IO.Abstractions;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Core.Infrastructure;
using Kudu.Services.Infrastructure;

namespace Kudu.Services.Zip
{
    // Extending VfsControllerBase is a slight abuse since this has nothing to do with vfs. But there is a lot
    // of good reusable logic in there. We could consider extracting a more basic base class from it.
    public class ZipController : VfsControllerBase
    {
        public ZipController(ITracer tracer, IEnvironment environment)
            : base(tracer, environment, environment.RootPath)
        {
        }

        protected override Task<HttpResponseMessage> CreateDirectoryGetResponse(DirectoryInfoBase info, string localFilePath)
        {
            HttpResponseMessage response = Request.CreateResponse();
            // GetQueryNameValuePairs returns an IEnumerable<KeyValuePair<string, string>>
            // KeyValuePair is a value type.
            var fileName = Request.GetQueryNameValuePairs().FirstOrDefault(p => p.Key.Equals("fileName", StringComparison.OrdinalIgnoreCase)).Value;
            fileName = fileName ?? Path.GetFileName(Path.GetDirectoryName(localFilePath)) + ".zip";
            response.Content = ZipStreamContent.Create(fileName, Tracer, zip =>
            {
                foreach (FileSystemInfoBase fileSysInfo in info.GetFileSystemInfos())
                {
                    var directoryInfo = fileSysInfo as DirectoryInfoBase;
                    if (directoryInfo != null)
                    {
                        zip.AddDirectory(directoryInfo, Tracer, fileSysInfo.Name);
                    }
                    else
                    {
                        // Add it at the root of the zip
                        zip.AddFile(fileSysInfo.FullName, Tracer, String.Empty);
                    }
                }
            });
            return Task.FromResult(response);
        }

        protected override Task<HttpResponseMessage> CreateItemGetResponse(FileSystemInfoBase info, string localFilePath)
        {
            // We don't support getting a file from the zip controller
            // Conceivably, it could be a zip file containing just the one file, but that's rarely interesting
            HttpResponseMessage notFoundResponse = Request.CreateResponse(HttpStatusCode.NotFound);
            return Task.FromResult(notFoundResponse);
        }

        protected override async Task<HttpResponseMessage> CreateDirectoryPutResponse(DirectoryInfoBase info, string localFilePath)
        {
            using (var stream = await Request.Content.ReadAsStreamAsync())
            {
                // The unzipping is done over the existing folder, without first removing existing files.
                // Hence it's more of a PATCH than a PUT. We should consider supporting both with the right semantic.
                // Though a true PUT at the root would be scary as it would wipe all existing files!
                var zipArchive = new ZipArchive(stream, ZipArchiveMode.Read);
                zipArchive.Extract(localFilePath, Tracer);
            }

            return Request.CreateResponse(HttpStatusCode.OK);
        }

        protected override Task<HttpResponseMessage> CreateItemPutResponse(FileSystemInfoBase info, string localFilePath, bool itemExists)
        {
            // We don't support putting an individual file using the zip controller
            HttpResponseMessage notFoundResponse = Request.CreateResponse(HttpStatusCode.NotFound);
            return Task.FromResult(notFoundResponse);
        }
    }
}
