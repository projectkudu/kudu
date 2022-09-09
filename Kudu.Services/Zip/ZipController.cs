using System;
using System.IO;
using System.IO.Abstractions;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Core.Deployment;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;
using Kudu.Services.Arm;
using Kudu.Services.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Kudu.Services.Zip
{
    // Extending VfsControllerBase is a slight abuse since this has nothing to do with vfs. But there is a lot
    // of good reusable logic in there. We could consider extracting a more basic base class from it.
    public class ZipController : VfsControllerBase
    {
        public ZipController(ITracer tracer, IEnvironment environment, IDeploymentSettingsManager settings)
            : base(tracer, environment, settings, environment.RootPath)
        {
        }

        protected override Task<HttpResponseMessage> CreateDirectoryGetResponse(IDirectoryInfo info, string localFilePath)
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

        protected override Task<HttpResponseMessage> CreateItemGetResponse(IFileSystemInfo info, string localFilePath)
        {
            // We don't support getting a file from the zip controller
            // Conceivably, it could be a zip file containing just the one file, but that's rarely interesting
            HttpResponseMessage notFoundResponse = Request.CreateResponse(HttpStatusCode.NotFound);
            return Task.FromResult(notFoundResponse);
        }

        protected override async Task<HttpResponseMessage> CreateDirectoryPutResponse(IDirectoryInfo info, string localFilePath)
        {
            try
            {
                var isRequestJSON = Request.Content.Headers?.ContentType?.MediaType?.Equals("application/json", StringComparison.OrdinalIgnoreCase);
                var targetPath = localFilePath;
                var isArmTemplate = false;
                JObject requestContent = null;
                Uri packageUri = null;
                if (isRequestJSON == true)
                {
                    requestContent = await Request.Content.ReadAsAsync<JObject>();
                    var payload = requestContent;
                    if (ArmUtils.IsArmRequest(Request))
                    {
                        payload = payload.Value<JObject>("properties");
                        isArmTemplate = ArmUtils.IsAzureResourceManagerUserAgent(Request);
                    }

                    var uri = payload?.Value<string>("packageUri");
                    if (!Uri.TryCreate(uri, UriKind.Absolute, out packageUri))
                    {
                        throw new InvalidOperationException($"Payload contains invalid '{uri}' packageUri property");
                    }

                    var path = payload?.Value<string>("path");
                    if (!string.IsNullOrEmpty(path))
                    {
                        targetPath = Path.Combine(targetPath, path);
                        FileSystemHelpers.CreateDirectory(targetPath);
                    }
                }

                using (packageUri == null ? Tracer.Step($"Extracting content to {targetPath}")
                    : Tracer.Step("Extracting content from {0} to {1}", StringUtils.ObfuscatePath(packageUri.AbsoluteUri), targetPath))
                {
                    var content = packageUri == null ? Request.Content
                        : await DeploymentHelper.GetArtifactContentFromURLAsync(new ArtifactDeploymentInfo(null, null) { RemoteURL = packageUri.AbsoluteUri }, Tracer);
                    using (var stream = await content.ReadAsStreamAsync())
                    {
                        // The unzipping is done over the existing folder, without first removing existing files.
                        // Hence it's more of a PATCH than a PUT. We should consider supporting both with the right semantic.
                        // Though a true PUT at the root would be scary as it would wipe all existing files!
                        var zipArchive = new ZipArchive(stream, ZipArchiveMode.Read);
                        zipArchive.Extract(targetPath, Tracer);
                    }
                }

                if (isArmTemplate && requestContent != null)
                {
                    requestContent.Value<JObject>("properties").Add("provisioningState", "Succeeded");
                    return Request.CreateResponse(HttpStatusCode.OK, requestContent);
                }

                return Request.CreateResponse(HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                return ArmUtils.CreateErrorResponse(Request, HttpStatusCode.BadRequest, ex);
            }
        }

        protected override Task<HttpResponseMessage> CreateItemPutResponse(IFileSystemInfo info, string localFilePath, bool itemExists)
        {
            // We don't support putting an individual file using the zip controller
            HttpResponseMessage notFoundResponse = Request.CreateResponse(HttpStatusCode.NotFound);
            return Task.FromResult(notFoundResponse);
        }
    }
}
