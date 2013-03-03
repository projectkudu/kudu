using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Kudu.Common;
using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Core.Deployment;
using Kudu.Core.Infrastructure;
using Kudu.Core.SourceControl;
using Kudu.Services.ByteRanges;
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

        // In case we already have a lock on the repository, we deploy an exponential
        // backoff algorithm, see http://en.wikipedia.org/wiki/Exponential_backoff. The backoff delays fall 
        // in a range 0ms to 1280ms. The table below represents 8 pre-generated backoff sequences (eight rows)
        // computed using Random with seed 0. When a request comes in and there is contention then it will
        // get assigned one of the 8 sequences and backoff accordingly. If there still is content after having
        // gone through all the delays then a 503 response is generated with a Retry-After header field 
        // indicating that the client can try again after the specified delay.
        private readonly static TimeSpan _retryAfter = TimeSpan.FromSeconds(2);
        private readonly static int[][] _delaySets = new int[][]
        {
            new int[] { 0, 32, 96, 48, 272, 176, 512, 3000 },
            new int[] { 16, 48, 64, 96, 0, 48, 1424, 1824 },
            new int[] { 0, 16, 32, 160, 224, 944, 288, 3000 },
            new int[] { 16, 32, 112, 208, 464, 816, 1216, 1840 },
            new int[] { 0, 48, 80, 0, 176, 688, 80, 3000 },
            new int[] { 16, 0, 32, 64, 416, 560, 992, 1856 },
            new int[] { 0, 16, 0, 112, 128, 432, 1920, 3000 },
            new int[] { 16, 48, 96, 176, 368, 304, 784, 1872 },
        };

        private readonly IDeploymentManager _deploymentManager;
        private readonly IOperationLock _operationLock;
        private readonly IRepository _repository;

        private EntityTagHeaderValue _currentEtag = null;
        private RepositoryItemStream _readStream = null;
        private bool _cleanupRebaseConflict;
        private int _delaySetIndex = 0;

        public LiveScmEditorController(ITracer tracer,
                                       IDeploymentManager deploymentManager,
                                       IOperationLock operationLock,
                                       IEnvironment environment,
                                       IRepository repository)
            : base(tracer, environment, environment.RepositoryPath)
        {
            _deploymentManager = deploymentManager;
            _operationLock = operationLock;
            _repository = repository;
            _currentEtag = GetCurrentEtag();
        }

        public override async Task<HttpResponseMessage> GetItem()
        {
            // Get a lock on the repository
            HttpResponseMessage busyResponse = await TryGetLock();
            if (busyResponse != null)
            {
                return busyResponse;
            }

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
            HttpResponseMessage busyResponse = await TryGetLock();
            if (busyResponse != null)
            {
                return busyResponse;
            }

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

        public override async Task<HttpResponseMessage> DeleteItem()
        {
            // Get a lock on the repository
            HttpResponseMessage busyResponse = await TryGetLock();
            if (busyResponse != null)
            {
                return busyResponse;
            }

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

        protected override Task<HttpResponseMessage> CreateItemGetResponse(FileSystemInfo info, string localFilePath)
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
            catch (Exception e)
            {
                // Could not read the file
                Tracer.TraceError(e);
                HttpResponseMessage errorResponse = Request.CreateErrorResponse(HttpStatusCode.NotFound, e);
                CloseReadStream();
                return Task.FromResult(errorResponse);
            }
        }

        protected override Task<HttpResponseMessage> CreateItemPutResponse(FileSystemInfo info, string localFilePath, bool itemExists)
        {
            // If repository is empty then there is no commit id and no master branch so we don't create any branch; we just init the repo.
            if (_currentEtag != null)
            {
                HttpResponseMessage errorResponse;
                if (!PrepareBranch(itemExists, out errorResponse))
                {
                    return Task.FromResult(errorResponse);
                }
            }
            else
            {
                // Initialize or re-initialize repository
                _repository.Initialize();
            }

            // Save file
            Stream fileStream = null;
            try
            {
                fileStream = GetFileWriteStream(localFilePath, fileExists: itemExists);
                return Request.Content.CopyToAsync(fileStream)
                    .Then(() =>
                    {
                        // Successfully saved the file
                        fileStream.Close();
                        fileStream = null;

                        // Use to track whether our rebase applied updates from master.
                        bool updateBranchIsUpToDate = false;

                        // Commit to local branch
                        ChangeSet commitResult = _repository.Commit(String.Format("Committing update from request {0}", Request.RequestUri), authorName: null);
                        if (commitResult == null)
                        {
                            HttpResponseMessage noChangeResponse = Request.CreateResponse(HttpStatusCode.NoContent);
                            noChangeResponse.Headers.ETag = CreateEtag(_repository.CurrentId);
                            return noChangeResponse;
                        }

                        if (_currentEtag != null)
                        {
                            try
                            {
                                // Rebase to get updates from master while checking whether we get a conflict
                                updateBranchIsUpToDate = _repository.Rebase(MasterBranch);

                                // Switch content back to master
                                _repository.UpdateRef(VfsUpdateBranch);

                                // Deploy update
                                _deploymentManager.Deploy(_repository, deployer: string.Empty);
                            }
                            catch (CommandLineException commandLineException)
                            {
                                Tracer.TraceError(commandLineException);

                                // The rebase resulted in a conflict. We send the conflicted version to the client so that the user
                                // can see the conflicts and resubmit.
                                _cleanupRebaseConflict = true;
                                HttpResponseMessage conflictResponse = Request.CreateResponse(HttpStatusCode.Conflict);
                                _readStream = new RepositoryItemStream(this, GetFileReadStream(localFilePath));
                                conflictResponse.Content = new StreamContent(_readStream, BufferSize);
                                conflictResponse.Content.Headers.ContentType = _conflictMediaType;
                                return conflictResponse;
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

                        // Set updated etag for the file
                        successFileResponse.Headers.ETag = CreateEtag(_repository.CurrentId);
                        return successFileResponse;
                    }, runSynchronously: true)
                    .Catch((catchInfo) =>
                    {
                        Tracer.TraceError(catchInfo.Exception);
                        HttpResponseMessage conflictResponse = Request.CreateErrorResponse(
                            HttpStatusCode.Conflict, RS.Format(Resources.VfsController_WriteConflict, localFilePath),
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
                Tracer.TraceError(e);
                HttpResponseMessage errorResponse = Request.CreateErrorResponse(HttpStatusCode.Conflict,
                    RS.Format(Resources.VfsController_WriteConflict, localFilePath), e);
                if (fileStream != null)
                {
                    fileStream.Close();
                }
                return Task.FromResult(errorResponse);
            }
        }

        protected override async Task<HttpResponseMessage> CreateItemDeleteResponse(FileSystemInfo info, string localFilePath)
        {
            HttpResponseMessage response;
            if (!PrepareBranch(true, out response))
            {
                return response;
            }

            response = await base.CreateItemDeleteResponse(info, localFilePath);

            // Use to track whether our rebase applied updates from master.
            bool updateBranchIsUpToDate = false;

            // Commit to local branch
            _repository.Commit(String.Format("Committing delete from request {0}", Request.RequestUri), authorName: null);

            try
            {
                // Rebase to get updates from master while checking whether we get a conflict
                updateBranchIsUpToDate = _repository.Rebase(MasterBranch);

                // Switch content back to master
                _repository.UpdateRef(VfsUpdateBranch);

                // Deploy update
                _deploymentManager.Deploy(_repository, deployer: string.Empty);
            }
            catch (CommandLineException commandLineException)
            {
                Tracer.TraceError(commandLineException);

                // Abort the ongoing rebase operation
                try
                {
                    _repository.RebaseAbort();
                }
                finally
                {
                    _repository.Update();
                }

                // The rebase resulted in a conflict.
                HttpResponseMessage conflictResponse = Request.CreateErrorResponse(HttpStatusCode.Conflict, commandLineException);
                return conflictResponse;
            }

            // Delete succeeded
            return response;
        }

        private async Task<HttpResponseMessage> TryGetLock()
        {
            int index = Interlocked.Increment(ref _delaySetIndex);
            int[] delaySet = _delaySets[index % _delaySets.Length];
            for (int cnt = 0; cnt < delaySet.Length; cnt++)
            {
                if (_operationLock.Lock())
                {
                    // We got a lock and can continue
                    return null;
                }

                // We didn't get a lock and so delay exponentially before trying again
                await Task.Delay(delaySet[cnt]);
            }

            // If we have gone through our delays and still can't get a lock then we give up and return 503
            HttpResponseMessage busyResponse = Request.CreateErrorResponse(HttpStatusCode.ServiceUnavailable, Resources.VfsController_Busy);
            busyResponse.Headers.RetryAfter = new RetryConditionHeaderValue(_retryAfter);
            return busyResponse;
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

                // If wild card match then set to current commit it
                startPoint = ifMatch != EntityTagHeaderValue.Any ? ifMatch.Tag.Trim(_quote) : _currentEtag.Tag.Trim(_quote);
            }

            try
            {
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
                _readStream = null;
            }
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
        /// Helper stream to ensure that we don't release the repository lock until we are finished
        /// sending all the data to the client. This avoids potential conflict situations where the lock has been
        /// released but the content is still being read from disk and served to the client. 
        /// </summary>
        private class RepositoryItemStream : DelegatingStream
        {
            private LiveScmEditorController _controller;

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
                }
            }
        }
    }
}
