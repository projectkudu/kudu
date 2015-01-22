using System;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
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
using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.SourceControl;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Core.Deployment;
using Kudu.Core.Infrastructure;
using Kudu.Core.SourceControl;
using Kudu.Core.Tracing;
using Kudu.Services.Infrastructure;

namespace Kudu.Services.SourceControl
{
    /// <summary>
    /// A Virtual File System controller which exposes GET, PUT, and DELETE for part of the Kudu file system
    /// which is backed by git.
    /// </summary>
    public class LiveScmEditorController : VfsControllerBase
    {
        private const string MasterBranch = "master";
        private const string VfsUpdateBranch = "VfsUpdateBranch";

        private readonly static MediaTypeHeaderValue _conflictMediaType = MediaTypeHeaderValue.Parse("text/plain");
        private readonly static char[] _quote = new char[] { '"' };

        private readonly IDeploymentManager _deploymentManager;
        private readonly IOperationLock _operationLock;
        private readonly IRepository _repository;

        private EntityTagHeaderValue _currentEtag = null;
        private RepositoryItemStream _readStream = null;
        private bool _cleanupRebaseConflict;

        public LiveScmEditorController(ITracer tracer,
                                       IDeploymentManager deploymentManager,
                                       IOperationLock operationLock,
                                       IEnvironment environment,
                                       IRepositoryFactory repositoryFactory)
            : base(tracer, environment, environment.RepositoryPath)
        {
            _deploymentManager = deploymentManager;
            _operationLock = operationLock;
            _repository = repositoryFactory.GetGitRepository();
        }

        public override async Task<HttpResponseMessage> GetItem()
        {
            // Get a lock on the repository
            await GetLockAsync();

            try
            {
                // Get current commit ID as etag. If null then repository is empty. Otherwise sync master to latest
                if (_currentEtag != null)
                {
                    _repository.Update();
                }

                // Get file
                return await base.GetItem();
            }
            catch (Exception e)
            {
                Tracer.TraceError(e);
                HttpResponseMessage errorResponse = Request.CreateErrorResponse(HttpStatusCode.InternalServerError, e);
                return errorResponse;
            }
            finally
            {
                // If we are sending data then RepositoryItemStream will release the lock
                if (_readStream == null)
                {
                    _operationLock.Release();
                }
            }
        }

        public override async Task<HttpResponseMessage> PutItem()
        {
            // Get a lock on the repository
            await GetLockAsync();

            try
            {
                // Update file
                HttpResponseMessage response = await base.PutItem();

                // If we are sending data then RepositoryItemStream will release the lock
                if (_readStream == null)
                {
                    _operationLock.Release();
                }

                return response;
            }
            catch (Exception e)
            {
                _operationLock.Release();
                Tracer.TraceError(e);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, e);
            }
        }

        public override async Task<HttpResponseMessage> DeleteItem(bool recursive = false)
        {
            if (recursive)
            {
                // Disallow recursive deletes when dealing with source control
                HttpResponseMessage errorResponse = Request.CreateResponse(HttpStatusCode.BadRequest);
                return errorResponse;
            }

            // Get a lock on the repository
            await GetLockAsync();

            try
            {
                // Get current commit ID as etag. If null then repository is empty. Otherwise sync master to latest
                if (_currentEtag != null)
                {
                    _repository.Update();
                }

                // Delete file
                return await base.DeleteItem();
            }
            catch (Exception e)
            {
                Tracer.TraceError(e);
                HttpResponseMessage errorResponse = Request.CreateErrorResponse(HttpStatusCode.InternalServerError, e);
                return errorResponse;
            }
            finally
            {
                _operationLock.Release();
            }
        }

        protected override Task<HttpResponseMessage> CreateItemGetResponse(FileSystemInfoBase info, string localFilePath)
        {
            // Check whether we have a conditional If-None-Match request
            if (IsIfNoneMatchRequest(_currentEtag))
            {
                HttpResponseMessage notModifiedResponse = Request.CreateResponse(HttpStatusCode.NotModified);
                notModifiedResponse.Headers.ETag = _currentEtag;
                return Task.FromResult(notModifiedResponse);
            }

            // Check whether we have a conditional range request containing both a Range and If-Range header field
            bool isRangeRequest = IsRangeRequest(_currentEtag);

            // Generate file response
            try
            {
                _readStream = new RepositoryItemStream(this, GetFileReadStream(localFilePath));
                MediaTypeHeaderValue mediaType = MediaTypeMap.GetMediaType(info.Extension);
                HttpResponseMessage successFileResponse = Request.CreateResponse(isRangeRequest ? HttpStatusCode.PartialContent : HttpStatusCode.OK);

                if (isRangeRequest)
                {
                    successFileResponse.Content = new ByteRangeStreamContent(_readStream, Request.Headers.Range, mediaType, BufferSize);
                }
                else
                {
                    successFileResponse.Content = new StreamContent(_readStream, BufferSize);
                    successFileResponse.Content.Headers.ContentType = mediaType;
                }

                // Set etag for the file
                successFileResponse.Headers.ETag = _currentEtag;
                return Task.FromResult(successFileResponse);
            }
            catch (InvalidByteRangeException invalidByteRangeException)
            {
                // The range request had no overlap with the current extend of the resource so generate a 416 (Requested Range Not Satisfiable)
                // including a Content-Range header with the current size.
                Tracer.TraceError(invalidByteRangeException);
                HttpResponseMessage invalidByteRangeResponse = Request.CreateErrorResponse(invalidByteRangeException);
                CloseReadStream();
                return Task.FromResult(invalidByteRangeResponse);
            }
            catch (Exception ex)
            {
                // Could not read the file
                Tracer.TraceError(ex);
                HttpResponseMessage errorResponse = Request.CreateErrorResponse(HttpStatusCode.NotFound, ex);
                CloseReadStream();
                return Task.FromResult(errorResponse);
            }
        }

        protected override async Task<HttpResponseMessage> CreateItemPutResponse(FileSystemInfoBase info, string localFilePath, bool itemExists)
        {
            // If repository is empty then there is no commit id and no master branch so we don't create any branch; we just init the repo.
            if (_currentEtag != null)
            {
                HttpResponseMessage errorResponse;
                if (!PrepareBranch(itemExists, out errorResponse))
                {
                    return errorResponse;
                }
            }
            else
            {
                // Initialize or re-initialize repository
                _repository.Initialize();
            }

            // Save file
            try
            {
                // Get the query parameters
                QueryParameters parameters = new QueryParameters(this.Request);

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
                            HttpStatusCode.Conflict, RS.Format(Resources.VfsController_WriteConflict, localFilePath, ex.Message),
                            ex);
                        return conflictResponse;
                    }
                }

                // Use to track whether our rebase applied updates from master.
                bool updateBranchIsUpToDate = true;

                // Commit to local branch
                bool commitResult = _repository.Commit(parameters.Message, authorName: null, emailAddress: null);
                if (!commitResult)
                {
                    HttpResponseMessage noChangeResponse = Request.CreateResponse(HttpStatusCode.NoContent);
                    noChangeResponse.Headers.ETag = CreateEtag(_repository.CurrentId);
                    return noChangeResponse;
                }

                bool rebasing = false;
                if (_currentEtag != null)
                {
                    try
                    {
                        // Only rebase if VFS branch isn't up-to-date already
                        if (!_repository.DoesBranchContainCommit(VfsUpdateBranch, MasterBranch))
                        {
                            // Rebase to get updates from master while checking whether we get a conflict
                            rebasing = true;
                            updateBranchIsUpToDate = _repository.Rebase(MasterBranch);
                        }

                        // Switch content back to master
                        _repository.UpdateRef(VfsUpdateBranch);
                    }
                    catch (CommandLineException commandLineException)
                    {
                        Tracer.TraceError(commandLineException);

                        if (rebasing)
                        {
                            // The rebase resulted in a conflict. We send the conflicted version to the client so that the user
                            // can see the conflicts and resubmit.
                            _cleanupRebaseConflict = true;
                            HttpResponseMessage conflictResponse = Request.CreateResponse(HttpStatusCode.Conflict);
                            _readStream = new RepositoryItemStream(this, GetFileReadStream(localFilePath));
                            conflictResponse.Content = new StreamContent(_readStream, BufferSize);
                            conflictResponse.Content.Headers.ContentType = _conflictMediaType;
                            return conflictResponse;
                        }
                        else
                        {
                            HttpResponseMessage updateErrorResponse =
                               Request.CreateErrorResponse(HttpStatusCode.InternalServerError, RS.Format(Resources.VfsScmUpdate_Error, commandLineException.Message));
                            return updateErrorResponse;
                        }
                    }
                }

                // If item does not already exist then we return 201 Created. Otherwise, as a successful commit could result 
                // in a non-conflicting merge we send back the committed version so that a client
                // can get the latest bits. This means we use a 200 OK response instead of a 204 response.
                HttpResponseMessage successFileResponse = null;
                if (itemExists)
                {
                    if (updateBranchIsUpToDate)
                    {
                        successFileResponse = Request.CreateResponse(HttpStatusCode.NoContent);
                    }
                    else
                    {
                        successFileResponse = Request.CreateResponse(HttpStatusCode.OK);
                        _readStream = new RepositoryItemStream(this, GetFileReadStream(localFilePath));
                        successFileResponse.Content = new StreamContent(_readStream, BufferSize);
                        successFileResponse.Content.Headers.ContentType = MediaTypeMap.GetMediaType(info.Extension);
                    }
                }
                else
                {
                    successFileResponse = Request.CreateResponse(HttpStatusCode.Created);
                }

                // Get current commit ID
                string currentId = _repository.CurrentId;

                // Deploy changes unless request indicated to not deploy
                if (!parameters.NoDeploy)
                {
                    DeployResult result = await DeployChangesAsync(currentId);
                    if (result != null && result.Status != DeployStatus.Success)
                    {
                        HttpResponseMessage deploymentErrorResponse =
                            Request.CreateErrorResponse(HttpStatusCode.InternalServerError, RS.Format(Resources.VfsScmController_DeploymentError, result.StatusText));
                        return deploymentErrorResponse;
                    }
                }

                // Set updated etag for the file
                successFileResponse.Headers.ETag = CreateEtag(currentId);
                return successFileResponse;
            }
            catch (Exception ex)
            {
                Tracer.TraceError(ex);
                HttpResponseMessage errorResponse = Request.CreateErrorResponse(HttpStatusCode.Conflict,
                    RS.Format(Resources.VfsController_WriteConflict, localFilePath, ex.Message), ex);
                return errorResponse;
            }
        }

        protected override async Task<HttpResponseMessage> CreateFileDeleteResponse(FileInfoBase info)
        {
            HttpResponseMessage response;
            if (!PrepareBranch(true, out response))
            {
                return response;
            }

            response = await base.CreateFileDeleteResponse(info);

            // Get the query parameters
            QueryParameters parameters = new QueryParameters(this.Request);

            // Commit to local branch
            _repository.Commit(parameters.Message, authorName: null, emailAddress: null);

            bool rebasing = false;
            try
            {
                // Only rebase if VFS branch isn't up-to-date already
                if (!_repository.DoesBranchContainCommit(VfsUpdateBranch, MasterBranch))
                {
                    // Rebase to get updates from master while checking whether we get a conflict
                    rebasing = true;
                    _repository.Rebase(MasterBranch);
                }

                // Switch content back to master
                _repository.UpdateRef(VfsUpdateBranch);
            }
            catch (CommandLineException commandLineException)
            {
                Tracer.TraceError(commandLineException);

                // Abort the ongoing rebase operation
                try
                {
                    if (rebasing)
                    {
                        _repository.RebaseAbort();
                    }
                }
                finally
                {
                    _repository.Update();
                }

                // The rebase resulted in a conflict.
                HttpResponseMessage conflictResponse = Request.CreateErrorResponse(HttpStatusCode.Conflict, commandLineException);
                return conflictResponse;
            }

            // Get current commit ID
            string currentId = _repository.CurrentId;

            // Deploy changes unless request indicated to not deploy
            if (!parameters.NoDeploy)
            {
                DeployResult result = await DeployChangesAsync(currentId);
                if (result != null && result.Status != DeployStatus.Success)
                {
                    HttpResponseMessage deploymentErrorResponse =
                        Request.CreateErrorResponse(HttpStatusCode.InternalServerError, RS.Format(Resources.VfsScmController_DeploymentError, result.StatusText));
                    return deploymentErrorResponse;
                }
            }

            // Delete succeeded. We add the etag as is has been updated as a result of the delete
            // This allows a client to keep track of the latest etag even for deletes.
            response.Headers.ETag = CreateEtag(currentId);
            return response;
        }

        private bool PrepareBranch(bool itemExists, out HttpResponseMessage errorResponse)
        {
            // Existing resources require an etag to be updated or deleted.
            string startPoint = String.Empty;
            if (itemExists)
            {
                if (Request.Headers.IfMatch == null || Request.Headers.IfMatch.Count != 1)
                {
                    errorResponse = Request.CreateErrorResponse(HttpStatusCode.PreconditionFailed, Resources.VfsScmController_MissingIfMatch);
                    errorResponse.Headers.ETag = _currentEtag;
                    return false;
                }

                EntityTagHeaderValue ifMatch = Request.Headers.IfMatch.First();
                if (ifMatch.IsWeak || ifMatch.Tag == null)
                {
                    errorResponse = Request.CreateErrorResponse(
                        HttpStatusCode.PreconditionFailed, RS.Format(Resources.VfsScmController_WeakEtag, ifMatch.ToString()));
                    errorResponse.Headers.ETag = _currentEtag;
                    return false;
                }

                // If wild card match then set to current commit ID
                startPoint = ifMatch != EntityTagHeaderValue.Any ? ifMatch.Tag.Trim(_quote) : _currentEtag.Tag.Trim(_quote);
            }

            try
            {
                // Clear out any un-staged files
                _repository.Clean();

                // Create or reset branch for this upload at the given starting point (commit ID)
                _repository.CreateOrResetBranch(VfsUpdateBranch, startPoint);
            }
            catch (Exception e)
            {
                Tracer.TraceError(e);
                errorResponse = Request.CreateErrorResponse(
                    HttpStatusCode.PreconditionFailed, RS.Format(Resources.VfsScmController_EtagMismatch, startPoint));
                errorResponse.Headers.ETag = _currentEtag;
                return false;
            }

            errorResponse = null;
            return true;
        }

        private async Task GetLockAsync()
        {
            await _operationLock.LockAsync();

            // Make sure we have the current commit ID
            _currentEtag = GetCurrentEtag();
         }

        private EntityTagHeaderValue GetCurrentEtag()
        {
            // If repository is completely empty then there is neither a current id nor a master branch.
            string currentId;
            try
            {
                currentId = _repository.CurrentId;
            }
            catch (Exception e)
            {
                Tracer.TraceError(e);
                return null;
            }
            return CreateEtag(currentId);
        }

        private void CloseReadStream()
        {
            if (_readStream != null)
            {
                _readStream.Close();
            }
        }

        private async Task<DeployResult> DeployChangesAsync(string commitId)
        {
            try
            {
                await _deploymentManager.DeployAsync(_repository, changeSet: null, deployer: string.Empty, clean: true, needFileUpdate: false);
            }
            catch (Exception e)
            {
                Tracer.TraceError(e);
            }

            // Inspect deployment for errors
            return _deploymentManager.GetResult(commitId);
        }

        private static EntityTagHeaderValue CreateEtag(string tag)
        {
            Contract.Assert(tag != null);
            StringBuilder result = new StringBuilder();
            result.Append("\"");
            result.Append(tag);
            result.Append("\"");
            return new EntityTagHeaderValue(result.ToString());
        }

        /// <summary>
        /// Contains optional query parameters passed in the request URI
        /// </summary>
        private class QueryParameters
        {
            public QueryParameters(HttpRequestMessage request)
            {
                if (request == null)
                {
                    throw new ArgumentNullException("request");
                }

                NameValueCollection queryParameters = request.RequestUri.ParseQueryString();
                Message = queryParameters["Message"] ?? String.Format("Committing update from request {0}", request.RequestUri.AbsolutePath);
                NoDeploy = GetBooleanValue(queryParameters["NoDeploy"]);
            }

            public string Message { get; private set; }

            public bool NoDeploy { get; private set; }

            [SuppressMessage("Microsoft.Performance", "CA1820:TestForEmptyStringsUsingStringLength", Justification = "We need to differentiate between null and empty string.")]
            private static bool GetBooleanValue(string value)
            {
                bool result;
                if (value == null)
                {
                    result = false;
                }
                else if (value == String.Empty)
                {
                    result = true;
                }
                else
                {
                    Boolean.TryParse(value, out result);
                }
                return result;
            }
        }

        /// <summary>
        /// Helper stream to ensure that we don't release the repository lock until we are finished
        /// sending all the data to the client. This avoids potential conflict situations where the lock has been
        /// released but the content is still being read from disk and served to the client. 
        /// </summary>
        private class RepositoryItemStream : DelegatingStream
        {
            private LiveScmEditorController _controller;
            private bool _disposed;

            internal RepositoryItemStream(LiveScmEditorController controller, Stream innerStream)
                : base(innerStream)
            {
                if (controller == null)
                {
                    throw new ArgumentNullException("controller");
                }
                _controller = controller;
            }

            public override void Close()
            {
                if (!_disposed)
                {
                    try
                    {
                        // Close the underlying stream
                        base.Close();

                        // Check to see if we have to undo an outstanding rebase. This will remove the conflicted
                        // data and revert back to what the commit looked like.
                        if (_controller._cleanupRebaseConflict)
                        {
                            try
                            {
                                _controller._repository.RebaseAbort();
                            }
                            finally
                            {
                                _controller._repository.Update();
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        _controller.Tracer.TraceError(e);
                    }
                    finally
                    {
                        // We are now completely done with this work item and can move on to the next one.
                        _controller._operationLock.Release();
                        _disposed = true;
                    }
                }
            }
        }
    }
}
