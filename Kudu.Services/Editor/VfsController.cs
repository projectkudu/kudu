using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Services.ByteRanges;
using Kudu.Services.Infrastructure;

namespace Kudu.Services.Editor
{
    /// <summary>
    /// A Virtual File System controller which exposes GET and PUT for the entire Kudu file system.
    /// </summary>
    public class VfsController : VfsControllerBase
    {
        public VfsController(ITracer tracer, IEnvironment environment)
            : base(tracer, environment, environment.RootPath)
        {
        }

        protected override HttpResponseMessage CreateItemGetResponse(FileSystemInfo info, string localFilePath)
        {
            // Get current etag
            EntityTagHeaderValue currentEtag = GetCurrentEtag(info);

            // Check whether we have a conditional If-None-Match request
            if (IsIfNoneMatchRequest(currentEtag))
            {
                HttpResponseMessage notModifiedResponse = Request.CreateResponse(HttpStatusCode.NotModified);
                notModifiedResponse.Headers.ETag = currentEtag;
                return notModifiedResponse;
            }

            // Check whether we have a conditional range request containing both a Range and If-Range header field
            bool isRangeRequest = IsRangeRequest(currentEtag);

            // Generate file response
            Stream fileStream = null;
            try
            {
                HttpResponseMessage successFileResponse = Request.CreateResponse(isRangeRequest ? HttpStatusCode.PartialContent : HttpStatusCode.OK);
                fileStream = GetFileReadStream(localFilePath);
                MediaTypeHeaderValue mediaType = MediaTypeMap.GetMediaType(info.Extension);

                if (isRangeRequest)
                {
                    successFileResponse.Content = new ByteRangeStreamContent(fileStream, Request.Headers.Range, mediaType, BufferSize);
                }
                else
                {
                    successFileResponse.Content = new StreamContent(fileStream, BufferSize);
                    successFileResponse.Content.Headers.ContentType = mediaType;
                }

                // Set etag for the file
                successFileResponse.Headers.ETag = currentEtag;
                return successFileResponse;
            }
            catch (InvalidByteRangeException invalidByteRangeException)
            {
                // The range request had no overlap with the current extend of the resource so generate a 416 (Requested Range Not Satisfiable)
                // including a Content-Range header with the current size.
                HttpResponseMessage invalidByteRangeResponse = Request.CreateErrorResponseRangeNotSatisfiable(invalidByteRangeException);
                if (fileStream != null)
                {
                    fileStream.Close();
                }
                return invalidByteRangeResponse;
            }
            catch (Exception e)
            {
                // Could not read the file
                HttpResponseMessage errorResponse = Request.CreateErrorResponse(HttpStatusCode.NotFound, e);
                if (fileStream != null)
                {
                    fileStream.Close();
                }
                return errorResponse;
            }
        }

        protected override Task<HttpResponseMessage> CreateItemPutResponse(FileSystemInfo info, string localFilePath, bool itemExists)
        {
            // Check that we have a matching conditional If-Match request for existing resources
            if (itemExists)
            {
                // Get current etag
                EntityTagHeaderValue currentEtag = GetCurrentEtag(info);

                // Existing resources require an etag to be updated.
                if (Request.Headers.IfMatch == null)
                {
                    HttpResponseMessage conflictDirectoryResponse = Request.CreateErrorResponse(
                        HttpStatusCode.PreconditionFailed,
                        "Updating an existing resource requires an If-Match header carrying an etag.");
                    return TaskHelpers.FromResult(conflictDirectoryResponse);
                }

                bool isMatch = false;
                foreach (EntityTagHeaderValue etag in Request.Headers.IfMatch)
                {
                    if (currentEtag.Equals(etag))
                    {
                        isMatch = true;
                        break;
                    }
                }

                if (!isMatch)
                {
                    HttpResponseMessage conflictFileResponse = Request.CreateErrorResponse(
                        HttpStatusCode.PreconditionFailed,
                        "Update is not based on the current etag");
                    conflictFileResponse.Headers.ETag = currentEtag;
                    return TaskHelpers.FromResult(conflictFileResponse);
                }
            }

            // Save file
            Stream fileStream = null;
            try
            {
                fileStream = GetFileWriteStream(localFilePath, itemExists);
                return Request.Content.CopyToAsync(fileStream)
                    .Then(() =>
                    {
                        // Successfully saved the file
                        fileStream.Close();
                        fileStream = null;

                        // Return either 204 No Content or 201 Created response
                        HttpResponseMessage successFileResponse =
                            Request.CreateResponse(itemExists ? HttpStatusCode.NoContent : HttpStatusCode.Created);

                        // Set updated etag for the file
                        successFileResponse.Headers.ETag = GetUpdatedEtag(localFilePath);
                        return successFileResponse;
                    })
                    .Catch((catchInfo) =>
                    {
                        HttpResponseMessage conflictResponse = Request.CreateErrorResponse(
                            HttpStatusCode.Conflict,
                            "Could not write to resource.",
                            catchInfo.Exception);

                        if (fileStream != null)
                        {
                            fileStream.Close();
                        }

                        return catchInfo.Handled(conflictResponse);
                    });

            }
            catch (Exception e)
            {
                HttpResponseMessage errorResponse = Request.CreateErrorResponse(HttpStatusCode.NotFound, e);
                if (fileStream != null)
                {
                    fileStream.Close();
                }
                return TaskHelpers.FromResult(errorResponse);
            }
        }

        private static EntityTagHeaderValue GetCurrentEtag(FileSystemInfo info)
        {
            return CreateEntityTag(info);
        }

        private static EntityTagHeaderValue GetUpdatedEtag(string localFilePath)
        {
            FileInfo fInfo = new FileInfo(localFilePath);
            return CreateEntityTag(fInfo);
        }

        /// <summary>
        /// Create unique etag based on the creation time and last modified type in UTC
        /// </summary>
        private static EntityTagHeaderValue CreateEntityTag(FileSystemInfo sysInfo)
        {
            Contract.Assert(sysInfo != null);
            byte[] etag = new byte[16];
            long cTime = sysInfo.CreationTimeUtc.Ticks;
            long lTime = sysInfo.LastWriteTimeUtc.Ticks;

            etag[0] = (byte)(cTime & 0xFF);
            etag[1] = (byte)((cTime >> 8) & 0xFF);
            etag[2] = (byte)((cTime >> 16) & 0xFF);
            etag[3] = (byte)((cTime >> 32) & 0xFF);
            etag[4] = (byte)((cTime >> 40) & 0xFF);
            etag[5] = (byte)((cTime >> 48) & 0xFF);
            etag[6] = (byte)((cTime >> 56) & 0xFF);
            etag[7] = (byte)((cTime >> 64) & 0xFF);

            etag[8] = (byte)(lTime & 0xFF);
            etag[9] = (byte)((lTime >> 8) & 0xFF);
            etag[10] = (byte)((lTime >> 16) & 0xFF);
            etag[11] = (byte)((lTime >> 32) & 0xFF);
            etag[12] = (byte)((lTime >> 40) & 0xFF);
            etag[13] = (byte)((lTime >> 48) & 0xFF);
            etag[14] = (byte)((lTime >> 56) & 0xFF);
            etag[15] = (byte)((lTime >> 64) & 0xFF);

            StringBuilder result = new StringBuilder();
            result.Append("\"");
            foreach (byte b in etag)
            {
                result.AppendFormat("{0:x2}", b);
            }
            result.Append("\"");
            return new EntityTagHeaderValue(result.ToString());
        }
    }
}
