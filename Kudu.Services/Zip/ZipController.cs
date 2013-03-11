using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Abstractions;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Services.Infrastructure;

namespace Kudu.Services.Deployment
{
    // Extending VfsControllerBase is a slight abuse since this has nothing to do with vfs. But there is a lot
    // of good reusable logic in there. We could consider extracting a more basic base class from it.
    public class ZipController : VfsControllerBase
    {
        public ZipController(ITracer tracer, IEnvironment environment)
            : base(tracer, environment, environment.RootPath)
        {
        }

        [SuppressMessage("Microsoft.Usage", "CA2202", Justification = "The ZipArchive is instantiated in a way that the stream is not closed on dispose")]
        protected override Task<HttpResponseMessage> CreateDirectoryGetResponse(DirectoryInfo info, string localFilePath)
        {
            HttpResponseMessage response = Request.CreateResponse();
            using (var ms = new MemoryStream())
            {
                using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
                {
                    foreach (FileSystemInfo fileSysInfo in info.EnumerateFileSystemInfos())
                    {
                        DirectoryInfo directoryInfo = fileSysInfo as DirectoryInfo;
                        if (directoryInfo != null)
                        {
                            zip.AddDirectory(new DirectoryInfoWrapper(directoryInfo), fileSysInfo.Name);
                        }
                        else
                        {
                            // Add it at the root of the zip
                            zip.AddFile(fileSysInfo.FullName, String.Empty);
                        }
                    }
                }
                response.Content = ms.AsContent();
            }

            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
            response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment");

            // Name the zip after the folder. e.g. "c:\foo\bar\" --> "bar"
            response.Content.Headers.ContentDisposition.FileName = Path.GetFileName(Path.GetDirectoryName(localFilePath)) + ".zip";
            return Task.FromResult(response);
        }

        protected override Task<HttpResponseMessage> CreateItemGetResponse(FileSystemInfo info, string localFilePath)
        {
            // We don't support getting a file from the zip controller
            // Conceivably, it could be a zip file containing just the one file, but that's rarely interesting
            HttpResponseMessage notFoundResponse = Request.CreateResponse(HttpStatusCode.NotFound);
            return Task.FromResult(notFoundResponse);
        }

        protected override async Task<HttpResponseMessage> CreateDirectoryPutResponse(DirectoryInfo info, string localFilePath)
        {
            using (var stream = await Request.Content.ReadAsStreamAsync())
            {
                // The unzipping is done over the existing folder, without first removing existing files.
                // Hence it's more of a PATCH than a PUT. We should consider supporting both with the right semantic.
                // Though a true PUT at the root would be scary as it would wipe all existing files!
                var zipArchive = new ZipArchive(stream, ZipArchiveMode.Read);
                zipArchive.Extract(new FileSystem(), localFilePath);
            }

            return Request.CreateResponse(HttpStatusCode.OK);
        }

        protected override Task<HttpResponseMessage> CreateItemPutResponse(FileSystemInfo info, string localFilePath, bool itemExists)
        {
            // We don't support putting an individual file using the zip controller
            HttpResponseMessage notFoundResponse = Request.CreateResponse(HttpStatusCode.NotFound);
            return Task.FromResult(notFoundResponse);
        }
    }
}
