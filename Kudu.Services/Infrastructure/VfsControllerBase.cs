using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Routing;
using Kudu.Contracts.Editor;
using Kudu.Contracts.Tracing;
using Kudu.Core;

namespace Kudu.Services.Infrastructure
{
    /// <summary>
    /// Provides common functionality for Virtual File System controllers.
    /// </summary>
    public abstract class VfsControllerBase : ApiController
    {
        private const string DirectoryEnumerationSearchPattern = "*";
        private const char UriSegmentSeparator = '/';

        private static readonly char[] _uriSegmentSeparator = new char[] { UriSegmentSeparator };
        private static readonly MediaTypeHeaderValue _directoryMediaType = MediaTypeHeaderValue.Parse("inode/directory");

        protected const int BufferSize = 32 * 1024;

        protected VfsControllerBase(ITracer tracer, IEnvironment environment, string rootPath)
        {
            if (rootPath == null)
            {
                throw new ArgumentNullException("rootPath");
            }
            Tracer = tracer;
            Environment = environment;
            RootPath = Path.GetFullPath(rootPath.TrimEnd(Path.DirectorySeparatorChar));
            MediaTypeMap = new MediaTypeMap();
        }

        [AcceptVerbs("GET", "HEAD")]
        public virtual HttpResponseMessage GetItem()
        {
            string localFilePath = GetLocalFilePath();

            DirectoryInfo info = new DirectoryInfo(localFilePath);
            if (info.Attributes < 0)
            {
                return Request.CreateResponse(HttpStatusCode.NotFound);
            }
            else if ((info.Attributes & FileAttributes.Directory) != 0)
            {
                // If request URI does NOT end in a "/" then redirect to one that does
                if (localFilePath[localFilePath.Length - 1] != Path.DirectorySeparatorChar)
                {
                    HttpResponseMessage redirectResponse = Request.CreateResponse(HttpStatusCode.TemporaryRedirect);
                    UriBuilder location = new UriBuilder(Request.RequestUri);
                    location.Path += "/";
                    redirectResponse.Headers.Location = location.Uri;
                    return redirectResponse;
                }
                else
                {
                    // Enumerate directory
                    IEnumerable<VfsStatEntry> directory = GetDirectoryResponse(info, localFilePath);
                    HttpResponseMessage successDirectoryResponse = Request.CreateResponse<IEnumerable<VfsStatEntry>>(HttpStatusCode.OK, directory);
                    return successDirectoryResponse;
                }
            }
            else
            {
                // If request URI ends in a "/" then redirect to one that does not
                if (localFilePath[localFilePath.Length - 1] == Path.DirectorySeparatorChar)
                {
                    HttpResponseMessage redirectResponse = Request.CreateResponse(HttpStatusCode.TemporaryRedirect);
                    UriBuilder location = new UriBuilder(Request.RequestUri);
                    location.Path = location.Path.TrimEnd(_uriSegmentSeparator);
                    redirectResponse.Headers.Location = location.Uri;
                    return redirectResponse;
                }

                // We are ready to get the file
                return CreateItemGetResponse(info, localFilePath);
            }
        }

        [HttpPut]
        public virtual Task<HttpResponseMessage> PutItem()
        {
            string localFilePath = GetLocalFilePath();
            DirectoryInfo info = new DirectoryInfo(localFilePath);
            bool itemExists = info.Attributes >= 0;

            if (itemExists && (info.Attributes & FileAttributes.Directory) != 0)
            {
                HttpResponseMessage conflictDirectoryResponse = Request.CreateErrorResponse(
                    HttpStatusCode.Conflict,
                    "The resource represents a directory which can not be updated.");
                return TaskHelpers.FromResult(conflictDirectoryResponse);
            }
            else
            {
                // If request URI ends in a "/" then redirect to one that does not
                if (localFilePath[localFilePath.Length - 1] == Path.DirectorySeparatorChar)
                {
                    HttpResponseMessage redirectResponse = Request.CreateResponse(HttpStatusCode.TemporaryRedirect);
                    UriBuilder location = new UriBuilder(Request.RequestUri);
                    location.Path = location.Path.TrimEnd(_uriSegmentSeparator);
                    redirectResponse.Headers.Location = location.Uri;
                    return TaskHelpers.FromResult(redirectResponse);
                }

                // We are ready to update the file
                return CreateItemPutResponse(info, localFilePath, itemExists);
            }
        }

        protected ITracer Tracer { get; private set; }

        protected IEnvironment Environment { get; private set; }

        protected string RootPath { get; private set; }

        protected MediaTypeMap MediaTypeMap { get; private set; }

        /// <summary>
        /// Indicates whether this is a conditional range request containing an
        /// If-Range header with a matching etag and a Range header indicating the 
        /// desired ranges
        /// </summary>
        protected bool IsRangeRequest(EntityTagHeaderValue currentEtag)
        {
            if (currentEtag != null && Request.Headers.IfRange != null && Request.Headers.Range != null)
            {
                // First check that the etag matches so that we can consider the range request
                if (currentEtag.Equals(Request.Headers.IfRange.EntityTag))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Indicates whether this is a If-None-Match request with a matching etag.
        /// </summary>
        protected bool IsIfNoneMatchRequest(EntityTagHeaderValue currentEtag)
        {
            if (currentEtag != null && Request.Headers.IfNoneMatch != null)
            {
                foreach (EntityTagHeaderValue etag in Request.Headers.IfNoneMatch)
                {
                    if (currentEtag.Equals(etag))
                    {
                        return true;
                    }
                }
            }
            return false;
        }


        protected abstract HttpResponseMessage CreateItemGetResponse(FileSystemInfo info, string localFilePath);

        protected abstract Task<HttpResponseMessage> CreateItemPutResponse(FileSystemInfo info, string localFilePath, bool itemExists);

        protected Stream GetFileReadStream(string localFilePath)
        {
            Contract.Assert(localFilePath != null);

            // Create StreamContent from FileStream. FileStream will get closed when StreamContent is closed
            return new FileStream(localFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);
        }

        protected Stream GetFileWriteStream(string localFilePath, bool fileExists)
        {
            Contract.Assert(localFilePath != null);

            // Create path if item doesn't already exist
            if (!fileExists)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(localFilePath));
            }

            // Create StreamContent from FileStream. FileStream will get closed when StreamContent is closed
            return new FileStream(localFilePath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true);
        }

        private string GetLocalFilePath()
        {
            IHttpRouteData routeData = Request.GetRouteData();
            string result = RootPath;
            if (routeData != null)
            {
                string path = routeData.Values["path"] as string;
                if (!String.IsNullOrEmpty(path))
                {
                    result = Path.GetFullPath(Path.Combine(result, path));
                }
                else
                {
                    string reqUri = Request.RequestUri.AbsoluteUri;
                    if (reqUri[reqUri.Length - 1] == UriSegmentSeparator)
                    {
                        result = Path.GetFullPath(result + Path.DirectorySeparatorChar);
                    }
                }
            }
            return result;
        }

        private IEnumerable<VfsStatEntry> GetDirectoryResponse(DirectoryInfo info, string localFilePath)
        {
            Contract.Assert(info != null);
            Contract.Assert(localFilePath != null);

            foreach (FileSystemInfo fileSysInfo in info.EnumerateFileSystemInfos(DirectoryEnumerationSearchPattern, SearchOption.TopDirectoryOnly))
            {
                FileInfo fInfo = fileSysInfo as FileInfo;
                bool isDirectory = (fileSysInfo.Attributes & FileAttributes.Directory) != 0;
                string mime = isDirectory ? _directoryMediaType.ToString() : MediaTypeMap.GetMediaType(fileSysInfo.Extension).ToString();
                string unescapedHref = isDirectory ? fileSysInfo.Name + UriSegmentSeparator : fileSysInfo.Name;
                yield return new VfsStatEntry
                {
                    Name = fileSysInfo.Name,
                    MTime = fileSysInfo.LastWriteTimeUtc,
                    Mime = mime,
                    Size = fInfo != null ? fInfo.Length : 0,
                    Href = Uri.EscapeUriString(unescapedHref),
                };
            }
        }
    }
}
