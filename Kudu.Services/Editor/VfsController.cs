using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Kudu.Common;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Core.Tracing;
using Kudu.Services.Infrastructure;

namespace Kudu.Services.Editor
{
    /// <summary>
    /// A Virtual File System controller which exposes GET, PUT, and DELETE for the entire Kudu file system.
    /// </summary>
    public class VfsController : VfsControllerBase
    {
        public VfsController(ITracer tracer, IEnvironment environment)
            : base(tracer, environment, environment.RootPath)
        {
        }

        protected override Task<HttpResponseMessage> CreateDirectoryPutResponse(DirectoryInfoBase info, string localFilePath)
        {
            if (info != null && info.Exists)
            {
                // Return a conflict result
                return base.CreateDirectoryPutResponse(info, localFilePath);
            }

            try
            {
                info.Create();
            }
            catch (IOException ex)
            {
                Tracer.TraceError(ex);
                HttpResponseMessage conflictDirectoryResponse = Request.CreateErrorResponse(
                    HttpStatusCode.Conflict, Resources.VfsControllerBase_CannotDeleteDirectory);
                return Task.FromResult(conflictDirectoryResponse);
            }

            // Return 201 Created response
            HttpResponseMessage successFileResponse = Request.CreateResponse(HttpStatusCode.Created);
            return Task.FromResult(successFileResponse);
        }

        protected override Task<HttpResponseMessage> CreateItemGetResponse(FileSystemInfoBase info, string localFilePath)
        {

            // Get current etag
            EntityTagHeaderValue currentEtag = CreateEntityTag(info);
            DateTime lastModified = info.LastWriteTimeUtc;

            // Check whether we have a range request (taking If-Range condition into account)
            bool isRangeRequest = IsRangeRequest(currentEtag);

            // Check whether we have a conditional If-None-Match request
            // Unless it is a range request (see RFC2616 sec 14.35.2 Range Retrieval Requests)
            if (!isRangeRequest && IsIfNoneMatchRequest(currentEtag))
            {
                HttpResponseMessage notModifiedResponse = Request.CreateResponse(HttpStatusCode.NotModified);
                notModifiedResponse.SetEntityTagHeader(currentEtag, lastModified);
                return Task.FromResult(notModifiedResponse);
            }

            // Generate file response
            Stream fileStream = null;
            try
            {
                fileStream = GetFileReadStream(localFilePath);
                MediaTypeHeaderValue mediaType = MediaTypeMap.GetMediaType(info.Extension);
                HttpResponseMessage successFileResponse = Request.CreateResponse(isRangeRequest ? HttpStatusCode.PartialContent : HttpStatusCode.OK);

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
                successFileResponse.SetEntityTagHeader(currentEtag, lastModified);
                return Task.FromResult(successFileResponse);
            }
            catch (InvalidByteRangeException invalidByteRangeException)
            {
                // The range request had no overlap with the current extend of the resource so generate a 416 (Requested Range Not Satisfiable)
                // including a Content-Range header with the current size.
                Tracer.TraceError(invalidByteRangeException);
                HttpResponseMessage invalidByteRangeResponse = Request.CreateErrorResponse(invalidByteRangeException);
                if (fileStream != null)
                {
                    fileStream.Close();
                }
                return Task.FromResult(invalidByteRangeResponse);
            }
            catch (Exception ex)
            {
                // Could not read the file
                Tracer.TraceError(ex);
                HttpResponseMessage errorResponse = Request.CreateErrorResponse(HttpStatusCode.NotFound, ex);
                if (fileStream != null)
                {
                    fileStream.Close();
                }
                return Task.FromResult(errorResponse);
            }
        }

        protected override async Task<HttpResponseMessage> CreateItemPutResponse(FileSystemInfoBase info, string localFilePath, bool itemExists)
        {
            // Check that we have a matching conditional If-Match request for existing resources
            if (itemExists)
            {
                // Get current etag
                EntityTagHeaderValue currentEtag = CreateEntityTag(info);

                // Existing resources require an etag to be updated.
                if (Request.Headers.IfMatch == null)
                {
                    HttpResponseMessage missingIfMatchResponse = Request.CreateErrorResponse(
                        HttpStatusCode.PreconditionFailed, Resources.VfsController_MissingIfMatch);
                    return missingIfMatchResponse;
                }

                bool isMatch = false;
                foreach (EntityTagHeaderValue etag in Request.Headers.IfMatch)
                {
                    if (currentEtag.Equals(etag) || etag == EntityTagHeaderValue.Any)
                    {
                        isMatch = true;
                        break;
                    }
                }

                if (!isMatch)
                {
                    HttpResponseMessage conflictFileResponse = Request.CreateErrorResponse(
                        HttpStatusCode.PreconditionFailed, Resources.VfsController_EtagMismatch);
                    conflictFileResponse.Headers.ETag = currentEtag;
                    return conflictFileResponse;
                }
            }

            // Save file
            try
            {
                using (Stream fileStream = GetFileWriteStream(localFilePath, fileExists: itemExists))
                {
                    try
                    {
                        await Request.Content.CopyToAsync(fileStream);
                    }
                    catch (Exception ex)
                    {
                        Tracer.TraceError(ex);
                        HttpResponseMessage conflictResponse = Request.CreateErrorResponse(
                            HttpStatusCode.Conflict,
                            RS.Format(Resources.VfsController_WriteConflict, localFilePath, ex.Message),
                            ex);

                        return conflictResponse;
                    }
                }

                // Return either 204 No Content or 201 Created response
                HttpResponseMessage successFileResponse =
                    Request.CreateResponse(itemExists ? HttpStatusCode.NoContent : HttpStatusCode.Created);

                // Set updated etag for the file
                info.Refresh();
                successFileResponse.SetEntityTagHeader(CreateEntityTag(info), info.LastWriteTimeUtc);
                return successFileResponse;

            }
            catch (Exception ex)
            {
                Tracer.TraceError(ex);
                HttpResponseMessage errorResponse =
                    Request.CreateErrorResponse(HttpStatusCode.Conflict,
                    RS.Format(Resources.VfsController_WriteConflict, localFilePath, ex.Message), ex);

                return errorResponse;
            }
        }

        protected override Task<HttpResponseMessage> CreateFileDeleteResponse(FileInfoBase info)
        {
            // Existing resources require an etag to be updated.
            if (Request.Headers.IfMatch == null)
            {
                HttpResponseMessage conflictDirectoryResponse = Request.CreateErrorResponse(
                    HttpStatusCode.PreconditionFailed, Resources.VfsController_MissingIfMatch);
                return Task.FromResult(conflictDirectoryResponse);
            }

            // Get current etag
            EntityTagHeaderValue currentEtag = CreateEntityTag(info);
            bool isMatch = Request.Headers.IfMatch.Any(etag => etag == EntityTagHeaderValue.Any || currentEtag.Equals(etag));

            if (!isMatch)
            {
                HttpResponseMessage conflictFileResponse = Request.CreateErrorResponse(
                    HttpStatusCode.PreconditionFailed, Resources.VfsController_EtagMismatch);
                conflictFileResponse.Headers.ETag = currentEtag;
                return Task.FromResult(conflictFileResponse);
            }

            return base.CreateFileDeleteResponse(info);
        }

        /// <summary>
        /// Create unique etag based on the last modified UTC time
        /// </summary>
        private static EntityTagHeaderValue CreateEntityTag(FileSystemInfoBase sysInfo)
        {
            Contract.Assert(sysInfo != null);
            byte[] etag = BitConverter.GetBytes(sysInfo.LastWriteTimeUtc.Ticks);

            var result = new StringBuilder(2 + etag.Length * 2);
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
