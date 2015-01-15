using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Kudu.Client.Editor;
using Kudu.Client.Infrastructure;
using Kudu.TestHarness;
using Xunit;

namespace Kudu.FunctionalTests
{
    public class VfsControllerBaseTest
    {
        private static readonly char _segmentDelimiter = '/';
        private static readonly char[] _segmentDelimiters = new char[] { _segmentDelimiter };

        private static readonly byte[] _fileContent0 = Encoding.UTF8.GetBytes("aaa\r\nbbb\r\nccc\r\n");
        private static readonly byte[] _fileContent1 = Encoding.UTF8.GetBytes("AAAA\r\nbbb\r\nccc\r\n");
        private static readonly byte[] _fileContent2 = Encoding.UTF8.GetBytes("aaa\r\nbbb\r\nCCCCC\r\n");
        private static readonly byte[] _fileContent3 = Encoding.UTF8.GetBytes("AAAA\r\nbbb\r\nCCCCC\r\n");
        private static readonly byte[] _fileContent4 = Encoding.UTF8.GetBytes("CCCCC\r\nbbb\r\nAAAAA\r\n");

        private static readonly ContentRangeHeaderValue _fileContentRange = new ContentRangeHeaderValue(_fileContent0.Length);

        private static readonly string _conflict = "<<<<<<< HEAD\r\nAAAA\r\nbbb\r\nCCCCC\r\n=======\r\nCCCCC\r\nbbb\r\nAAAAA\r\n>>>>>>>";

        private static readonly MediaTypeHeaderValue _fileMediaType = MediaTypeHeaderValue.Parse("text/plain");
        private static readonly MediaTypeHeaderValue _dirMediaType = MediaTypeHeaderValue.Parse("application/json; charset=utf-8");
        private static readonly MediaTypeHeaderValue _conflictMediaType = MediaTypeHeaderValue.Parse("text/plain");

        private bool _testConflictingUpdates;
        private bool _isScmEditorTest;

        public VfsControllerBaseTest(RemoteVfsManager client, bool testConflictingUpdates, RemoteVfsManager deploymentClient = null)
        {
            KuduClient = client;
            Client = client.Client;
            BaseAddress = Client.BaseAddress.GetComponents(UriComponents.HttpRequestUrl, UriFormat.Unescaped).TrimEnd(_segmentDelimiters);

            if (deploymentClient != null)
            {
                DeploymentClient = deploymentClient.Client;
                DeploymentBaseAddress = DeploymentClient.BaseAddress.GetComponents(UriComponents.HttpRequestUrl, UriFormat.Unescaped).TrimEnd(_segmentDelimiters);
            }

            _testConflictingUpdates = testConflictingUpdates;
            _isScmEditorTest = deploymentClient != null;
        }

        protected string BaseAddress { get; private set; }

        protected RemoteVfsManager KuduClient { get; private set; }

        protected HttpClient Client { get; private set; }

        protected HttpClient DeploymentClient { get; private set; }

        protected string DeploymentBaseAddress { get; private set; }

        public virtual async Task RunIntegrationTest()
        {
            string dir = Guid.NewGuid().ToString("N");
            string dirAddress = BaseAddress + _segmentDelimiter + dir;
            string dirAddressWithTerminatingSlash = dirAddress + _segmentDelimiter;

            string file = Guid.NewGuid().ToString("N") + ".txt";
            string fileAddress = dirAddressWithTerminatingSlash + file;
            string fileAddressWithTerminatingSlash = fileAddress + _segmentDelimiter;

            string deploymentFileAddress = null;
            string customDeploymentFileAddress = null;
            if (DeploymentClient != null)
            {
                deploymentFileAddress = string.Format("{0}{1}site{1}wwwroot{1}{2}{1}{3}", DeploymentBaseAddress, _segmentDelimiter, dir, file);
                customDeploymentFileAddress = string.Format("{0}{1}site{1}wwwroot{1}test{1}{2}{1}{3}", DeploymentBaseAddress, _segmentDelimiter, dir, file);
            }

            TestTracer.Trace("Starting RunIntegrationTest");
            TestTracer.Trace("Dir - {0}", dirAddress);
            TestTracer.Trace("File - {0}", fileAddress);
            TestTracer.Trace("DeploymentFileAddress - {0}", deploymentFileAddress);

            HttpResponseMessage response;

            // Check not found file responses
            TestTracer.Trace("==== Check not found file responses");
            response = await HttpGetAsync(dirAddress);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

            response = await HttpGetAsync(dirAddressWithTerminatingSlash);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

            response = await HttpGetAsync(fileAddress);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

            response = await HttpGetAsync(fileAddressWithTerminatingSlash);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

            // Check create file results in 201 response with etag
            TestTracer.Trace("==== Check create file results in 201 response with etag");
            response = await HttpPutAsync(fileAddress, CreateUploadContent(_fileContent0));
            await VerifyDeployment(deploymentFileAddress, HttpStatusCode.OK, _fileContent0);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            EntityTagHeaderValue originalEtag = response.Headers.ETag;
            Assert.NotNull(originalEtag);

            DateTimeOffset? lastModified = response.Content.Headers.LastModified;
            if (!_isScmEditorTest)
            {
                Assert.NotNull(lastModified);
            }

            // Check that we get a 200 (OK) on created file with the correct etag
            TestTracer.Trace("==== Check that we get a 200 (OK) on created file with the correct etag");
            response = await HttpGetAsync(fileAddress);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(originalEtag, response.Headers.ETag);
            Assert.Equal(lastModified, response.Content.Headers.LastModified);
            Assert.Equal(_fileMediaType, response.Content.Headers.ContentType);

            // Check that we get a 200 (OK) on created file using HEAD with the correct etag
            TestTracer.Trace("==== Check that we get a 200 (OK) on created file using HEAD with the correct etag");
            using (HttpRequestMessage headReq = new HttpRequestMessage())
            {
                headReq.Method = HttpMethod.Head;
                headReq.RequestUri = new Uri(fileAddress);
                response = await Client.SendAsync(headReq);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Equal(originalEtag, response.Headers.ETag);
                Assert.Equal(_fileMediaType, response.Content.Headers.ContentType);
            }

            // Check that we get a 304 (Not Modified) response if matching If-None-Match
            TestTracer.Trace("==== Check that we get a 304 (Not Modified) response if matching If-None-Match");
            using (HttpRequestMessage ifNoneMatchReq = new HttpRequestMessage())
            {
                ifNoneMatchReq.RequestUri = new Uri(fileAddress);
                ifNoneMatchReq.Headers.IfNoneMatch.Add(originalEtag);
                response = await HttpSendAsync(ifNoneMatchReq);
                Assert.Equal(HttpStatusCode.NotModified, response.StatusCode);
                Assert.Equal(originalEtag, response.Headers.ETag);
            }

            // Check that we get a 200 (OK) response if not matching If-None-Match
            TestTracer.Trace("==== Check that we get a 200 (OK) response if not matching If-None-Match");
            using (HttpRequestMessage ifNoneMatchReqBadEtag = new HttpRequestMessage())
            {
                ifNoneMatchReqBadEtag.RequestUri = new Uri(fileAddress);
                ifNoneMatchReqBadEtag.Headers.IfNoneMatch.Add(new EntityTagHeaderValue("\"NotMatching\""));
                response = await HttpSendAsync(ifNoneMatchReqBadEtag);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Equal(originalEtag, response.Headers.ETag);
            }

            // Check that If-Range request with range returns 206 (Partial Content)
            TestTracer.Trace("==== Check that If-Range request with range returns 206 (Partial Content)");
            using (HttpRequestMessage ifRangeReq = new HttpRequestMessage())
            {
                ifRangeReq.RequestUri = new Uri(fileAddress);
                ifRangeReq.Headers.IfRange = new RangeConditionHeaderValue(originalEtag);
                ifRangeReq.Headers.Range = new RangeHeaderValue(0, 0) { Unit = "bytes" };
                response = await HttpSendAsync(ifRangeReq);
                Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);
                Assert.Equal(originalEtag, response.Headers.ETag);
                Assert.Equal(1, response.Content.Headers.ContentLength);
                Assert.Equal(new ContentRangeHeaderValue(0, 0, _fileContent0.Length), response.Content.Headers.ContentRange);
            }

            // Check that If-Range request with no range returns 200 (OK)
            TestTracer.Trace("==== Check that If-Range request with no range returns 200 (OK)");
            using (HttpRequestMessage ifRangeReqNoRange = new HttpRequestMessage())
            {
                ifRangeReqNoRange.RequestUri = new Uri(fileAddress);
                ifRangeReqNoRange.Headers.IfRange = new RangeConditionHeaderValue(originalEtag);
                response = await HttpSendAsync(ifRangeReqNoRange);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Equal(originalEtag, response.Headers.ETag);
            }

            // Check that If-Range request with bad range returns 416 (Requested Range Not Satisfiable)
            // including a Content-Range header
            TestTracer.Trace("==== Check that If-Range request with bad range returns 416 (Requested Range Not Satisfiable)");
            using (HttpRequestMessage ifRangeReqBadRange = new HttpRequestMessage())
            {
                ifRangeReqBadRange.RequestUri = new Uri(fileAddress);
                ifRangeReqBadRange.Headers.IfRange = new RangeConditionHeaderValue(originalEtag);
                ifRangeReqBadRange.Headers.Range = new RangeHeaderValue(100, 100) { Unit = "bytes" };
                response = await HttpSendAsync(ifRangeReqBadRange);
                Assert.Equal(HttpStatusCode.RequestedRangeNotSatisfiable, response.StatusCode);
                Assert.Equal(_fileContentRange, response.Content.Headers.ContentRange);
            }

            // Check that we get a root directory view
            TestTracer.Trace("==== Check that we get a root directory view");
            response = await HttpGetAsync(BaseAddress);
            Assert.Equal(_dirMediaType, response.Content.Headers.ContentType);

            // Check that we get a directory view from folder
            TestTracer.Trace("==== Check that we get a directory view from folder");
            response = await HttpGetAsync(dirAddress);
            Assert.Equal(_dirMediaType, response.Content.Headers.ContentType);

            // Check various redirects between files and folders
            HttpClientHandler redirectHandler = HttpClientHelper.CreateClientHandler(BaseAddress, KuduClient.Credentials);
            redirectHandler.AllowAutoRedirect = false;
            using (HttpClient redirectClient = HttpClientHelper.CreateClient(BaseAddress, KuduClient.Credentials, redirectHandler))
            {
                // Ensure that requests to root without slash is redirected to one with slash
                TestTracer.Trace("==== Ensure that requests to root without slash is redirected to one with slash");
                response = await HttpGetAsync(BaseAddress, redirectClient);
                Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);
                Assert.Equal(BaseAddress + _segmentDelimiter, response.Headers.Location.AbsoluteUri);

                // Ensure that requests to directory without slash is redirected to one with slash
                TestTracer.Trace("==== Ensure that requests to directory without slash is redirected to one with slash");
                response = await HttpGetAsync(dirAddress, redirectClient);
                Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);
                Assert.Equal(dirAddressWithTerminatingSlash, response.Headers.Location.AbsoluteUri);

                // Ensure that requests to file with slash is redirected to one without slash
                TestTracer.Trace("==== Ensure that requests to file with slash is redirected to one without slash");
                response = await HttpGetAsync(fileAddressWithTerminatingSlash, redirectClient);
                Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);
                Assert.Equal(fileAddress, response.Headers.Location.AbsoluteUri);
            }

            // Check that 2nd create attempt fails
            TestTracer.Trace("==== Check that 2nd create attempt fails");
            response = await HttpPutAsync(fileAddress, CreateUploadContent(_fileContent0));
            Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
            Assert.Equal(originalEtag, response.Headers.ETag);

            // Check that we can't update a directory
            TestTracer.Trace("==== Check that we can't update a directory");
            response = await HttpPutAsync(dirAddress, CreateUploadContent(_fileContent0));
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

            // Check that we can't delete a non-empty directory
            TestTracer.Trace("==== Check that we can't delete a non-empty directory");
            response = await HttpDeleteAsync(dirAddress);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

            EntityTagHeaderValue updatedEtag;
            if (_testConflictingUpdates)
            {
                // Update file with first edit based on original etag
                TestTracer.Trace("==== Update file with first edit based on original etag");
                using (HttpRequestMessage update1 = new HttpRequestMessage())
                {
                    update1.Method = HttpMethod.Put;
                    update1.RequestUri = new Uri(fileAddress);
                    update1.Headers.IfMatch.Add(originalEtag);
                    update1.Content = CreateUploadContent(_fileContent1);

                    response = await HttpSendAsync(update1);
                    await VerifyDeployment(deploymentFileAddress, HttpStatusCode.OK, _fileContent1);

                    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
                    Assert.NotNull(response.Headers.ETag);
                    Assert.NotEqual(originalEtag, response.Headers.ETag);
                    updatedEtag = response.Headers.ETag;
                }

                // Update file with second edit based on original etag (non-conflicting merge)
                TestTracer.Trace("==== Update file with second edit based on original etag (non-conflicting merge)");
                using (HttpRequestMessage update2 = new HttpRequestMessage())
                {
                    update2.Method = HttpMethod.Put;
                    update2.RequestUri = new Uri(fileAddress);
                    update2.Headers.IfMatch.Add(originalEtag);
                    update2.Content = CreateUploadContent(_fileContent2);

                    response = await HttpSendAsync(update2);
                    await VerifyDeployment(deploymentFileAddress, HttpStatusCode.OK, _fileContent3);

                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                    Assert.NotNull(response.Headers.ETag);
                    Assert.NotEqual(updatedEtag, response.Headers.ETag);
                    Assert.Equal(_fileMediaType, response.Content.Headers.ContentType);
                    updatedEtag = response.Headers.ETag;
                }

                // Update file with third edit based on original etag (non-conflicting merge)
                TestTracer.Trace("==== Update file with third edit based on original etag (non-conflicting merge)");
                using (HttpRequestMessage update3 = new HttpRequestMessage())
                {
                    update3.Method = HttpMethod.Put;
                    update3.RequestUri = new Uri(fileAddress);
                    update3.Headers.IfMatch.Add(originalEtag);
                    update3.Content = CreateUploadContent(_fileContent3);

                    response = await HttpSendAsync(update3);
                    await VerifyDeployment(deploymentFileAddress, HttpStatusCode.OK, _fileContent3);

                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                    Assert.NotNull(response.Headers.ETag);
                    Assert.Equal(updatedEtag, response.Headers.ETag);
                    Assert.Equal(_fileMediaType, response.Content.Headers.ContentType);
                    updatedEtag = response.Headers.ETag;
                }

                // Update file with forth edit based on original etag (conflicting)
                TestTracer.Trace("==== Update file with forth edit based on original etag (conflicting)");
                using (HttpRequestMessage update4 = new HttpRequestMessage())
                {
                    update4.Method = HttpMethod.Put;
                    update4.RequestUri = new Uri(fileAddress);
                    update4.Headers.IfMatch.Add(originalEtag);
                    update4.Content = CreateUploadContent(_fileContent4);

                    response = await HttpSendAsync(update4);
                    await VerifyDeployment(deploymentFileAddress, HttpStatusCode.OK, _fileContent3);

                    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
                    Assert.Equal(_conflictMediaType, response.Content.Headers.ContentType);
                    Assert.Null(response.Headers.ETag);
                    string content = await response.Content.ReadAsStringAsync();
                    Assert.True(content.StartsWith(_conflict));
                }

                // Update file with fifth edit based on invalid etag
                TestTracer.Trace("==== Update file with fifth edit based on invalid etag");
                using (HttpRequestMessage update5 = new HttpRequestMessage())
                {
                    update5.Method = HttpMethod.Put;
                    update5.RequestUri = new Uri(fileAddress);
                    update5.Headers.IfMatch.Add(new EntityTagHeaderValue("\"invalidetag\""));
                    update5.Content = CreateUploadContent(_fileContent1);

                    response = await HttpSendAsync(update5);
                    await VerifyDeployment(deploymentFileAddress, HttpStatusCode.OK, _fileContent3);

                    Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
                    Assert.Equal(updatedEtag, response.Headers.ETag);
                }

                // Check that update with wildcard etag succeeds
                TestTracer.Trace("==== Check that update with wildcard etag succeeds");
                using (HttpRequestMessage update6 = new HttpRequestMessage())
                {
                    update6.Method = HttpMethod.Put;
                    update6.RequestUri = new Uri(fileAddress);
                    update6.Headers.IfMatch.Add(EntityTagHeaderValue.Any);
                    update6.Content = CreateUploadContent(_fileContent1);

                    response = await HttpSendAsync(update6);
                    await VerifyDeployment(deploymentFileAddress, HttpStatusCode.OK, _fileContent1);

                    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
                    Assert.NotNull(response.Headers.ETag);
                    Assert.NotEqual(originalEtag, response.Headers.ETag);
                    updatedEtag = response.Headers.ETag;
                }

                TestTracer.Trace("==== Check that concurrent updates work");
                List<Task<HttpResponseMessage>> concurrentUpdates = new List<Task<HttpResponseMessage>>();
                for (int cnt = 0; cnt < 16; cnt++)
                {
                    HttpRequestMessage concurrentRequest = new HttpRequestMessage()
                    {
                        Method = HttpMethod.Put,
                        RequestUri = new Uri(fileAddress + "?nodeploy"),
                        Content = CreateUploadContent(_fileContent2)
                    };

                    concurrentRequest.Headers.IfMatch.Add(EntityTagHeaderValue.Any);
                    concurrentUpdates.Add(HttpSendAsync(concurrentRequest));
                }
                await Task.WhenAll(concurrentUpdates);
                IEnumerable<HttpResponseMessage> concurrentResponses = concurrentUpdates.Select(update => update.Result);
                foreach (HttpResponseMessage concurrentResponse in concurrentResponses)
                {
                    Assert.Equal(HttpStatusCode.NoContent, concurrentResponse.StatusCode);
                }

                TestTracer.Trace("==== Check that 'nodeploy' doesn't deploy and that the old content remains.");
                using (HttpRequestMessage request = new HttpRequestMessage())
                {
                    request.Method = HttpMethod.Put;
                    request.RequestUri = new Uri(fileAddress + "?nodeploy");
                    request.Headers.IfMatch.Add(EntityTagHeaderValue.Any);
                    request.Content = CreateUploadContent(_fileContent2);

                    response = await HttpSendAsync(request);
                    await VerifyDeployment(deploymentFileAddress, HttpStatusCode.OK, _fileContent1);

                    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
                    Assert.NotNull(response.Headers.ETag);
                    Assert.NotEqual(originalEtag, response.Headers.ETag);
                    updatedEtag = response.Headers.ETag;
                }

                TestTracer.Trace("==== Check passing custom commit message.");
                using (HttpRequestMessage request = new HttpRequestMessage())
                {
                    request.Method = HttpMethod.Put;
                    request.RequestUri = new Uri(fileAddress + "?message=helloworld");
                    request.Headers.IfMatch.Add(EntityTagHeaderValue.Any);
                    request.Content = CreateUploadContent(_fileContent3);

                    response = await HttpSendAsync(request);
                    await VerifyDeployment(deploymentFileAddress, HttpStatusCode.OK, _fileContent3);

                    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
                    Assert.NotNull(response.Headers.ETag);
                    Assert.NotEqual(originalEtag, response.Headers.ETag);
                    updatedEtag = response.Headers.ETag;
                }

                TestTracer.Trace("==== Check that custom deployment script works");
                using (HttpRequestMessage update7 = new HttpRequestMessage())
                {
                    // Upload custom deployment scripts
                    TestTracer.Trace("==== Upload custom deployment scripts");
                    updatedEtag = await UploadCustomDeploymentScripts();

                    // Upload content and validate that it gets deployed
                    TestTracer.Trace("==== Upload content and validate that it gets deployed");
                    update7.Method = HttpMethod.Put;
                    update7.RequestUri = new Uri(fileAddress);
                    update7.Headers.IfMatch.Add(updatedEtag);
                    update7.Content = CreateUploadContent(_fileContent2);

                    response = await HttpSendAsync(update7);
                    await VerifyDeployment(customDeploymentFileAddress, HttpStatusCode.OK, _fileContent2);

                    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
                    Assert.NotNull(response.Headers.ETag);
                    Assert.NotEqual(originalEtag, response.Headers.ETag);
                    updatedEtag = response.Headers.ETag;

                    // Remove custom deployment scripts
                    TestTracer.Trace("==== Remove custom deployment scripts");
                    updatedEtag = await RemoveCustomDeploymentScripts();
                }

                // Check that delete with invalid etag fails
                TestTracer.Trace("==== Check that delete with invalid etag fails");
                using (HttpRequestMessage deleteRequest = new HttpRequestMessage())
                {
                    deleteRequest.Method = HttpMethod.Delete;
                    deleteRequest.RequestUri = new Uri(fileAddress);
                    deleteRequest.Headers.IfMatch.Add(new EntityTagHeaderValue("\"invalidetag\""));

                    response = await HttpSendAsync(deleteRequest);
                    await VerifyDeployment(deploymentFileAddress, HttpStatusCode.OK, _fileContent2);

                    Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
                    Assert.Equal(updatedEtag, response.Headers.ETag);
                }

                // Check that delete with conflict fails
                TestTracer.Trace("==== Check that delete with conflict fails");
                using (HttpRequestMessage deleteRequest = new HttpRequestMessage())
                {
                    deleteRequest.Method = HttpMethod.Delete;
                    deleteRequest.RequestUri = new Uri(fileAddress);
                    deleteRequest.Headers.IfMatch.Add(originalEtag);

                    response = await HttpSendAsync(deleteRequest);
                    await VerifyDeployment(deploymentFileAddress, HttpStatusCode.OK, _fileContent2);

                    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
                }

                // Check that delete with valid etag succeeds
                TestTracer.Trace("==== Check that delete with valid etag succeeds");
                using (HttpRequestMessage deleteRequest = new HttpRequestMessage())
                {
                    deleteRequest.Method = HttpMethod.Delete;
                    deleteRequest.RequestUri = new Uri(fileAddress);
                    deleteRequest.Headers.IfMatch.Add(updatedEtag);

                    response = await HttpSendAsync(deleteRequest);
                    await VerifyDeployment(deploymentFileAddress, HttpStatusCode.NotFound, null);

                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                }

                // Check that 2nd delete attempt fails
                TestTracer.Trace("==== Check that 2nd delete attempt fails");
                using (HttpRequestMessage deleteRequest = new HttpRequestMessage())
                {
                    deleteRequest.Method = HttpMethod.Delete;
                    deleteRequest.RequestUri = new Uri(fileAddress);
                    deleteRequest.Headers.IfMatch.Add(updatedEtag);

                    response = await HttpSendAsync(deleteRequest);
                    await VerifyDeployment(deploymentFileAddress, HttpStatusCode.NotFound, null);

                    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                }
            }
            else
            {
                // Check that update with correct etag generates 204 Response with new etag
                TestTracer.Trace("==== Check that update with correct etag generates 204 Response with new etag");
                using (HttpRequestMessage updateRequest = new HttpRequestMessage())
                {
                    updateRequest.Method = HttpMethod.Put;
                    updateRequest.RequestUri = new Uri(fileAddress);
                    updateRequest.Headers.IfMatch.Add(originalEtag);
                    updateRequest.Content = CreateUploadContent(_fileContent1);
                    response = await HttpSendAsync(updateRequest);
                    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
                    Assert.NotNull(response.Headers.ETag);
                    Assert.NotEqual(originalEtag, response.Headers.ETag);
                    updatedEtag = response.Headers.ETag;
                }

                // Check that 2nd create attempt fails
                TestTracer.Trace("==== Check that 2nd create attempt fails");
                using (HttpRequestMessage updateRequest = new HttpRequestMessage())
                {
                    updateRequest.Method = HttpMethod.Put;
                    updateRequest.RequestUri = new Uri(fileAddress);
                    updateRequest.Headers.IfMatch.Add(originalEtag);
                    updateRequest.Content = CreateUploadContent(_fileContent2);
                    response = await HttpSendAsync(updateRequest);
                    Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
                    Assert.Equal(updatedEtag, response.Headers.ETag);
                }

                // Check that update with invalid etag fails
                TestTracer.Trace("==== Check that update with invalid etag fails");
                using (HttpRequestMessage updateRequest = new HttpRequestMessage())
                {
                    updateRequest.Method = HttpMethod.Put;
                    updateRequest.RequestUri = new Uri(fileAddress);
                    updateRequest.Headers.IfMatch.Add(new EntityTagHeaderValue("\"invalidetag\""));
                    updateRequest.Content = CreateUploadContent(_fileContent1);
                    response = await HttpSendAsync(updateRequest);
                    Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
                    Assert.Equal(updatedEtag, response.Headers.ETag);
                }

                // Check that update with wildcard etag succeeds
                TestTracer.Trace("==== Check that update with wildcard etag succeeds");
                using (HttpRequestMessage updateRequest = new HttpRequestMessage())
                {
                    updateRequest.Method = HttpMethod.Put;
                    updateRequest.RequestUri = new Uri(fileAddress);
                    updateRequest.Headers.IfMatch.Add(EntityTagHeaderValue.Any);
                    updateRequest.Content = CreateUploadContent(_fileContent1);
                    response = await HttpSendAsync(updateRequest);
                    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
                    Assert.NotNull(response.Headers.ETag);
                    Assert.NotEqual(originalEtag, response.Headers.ETag);
                    updatedEtag = response.Headers.ETag;
                }

                // Check that delete with invalid etag fails
                TestTracer.Trace("==== Check that delete with invalid etag fails");
                using (HttpRequestMessage deleteRequest = new HttpRequestMessage())
                {
                    deleteRequest.Method = HttpMethod.Delete;
                    deleteRequest.RequestUri = new Uri(fileAddress);
                    deleteRequest.Headers.IfMatch.Add(new EntityTagHeaderValue("\"invalidetag\""));
                    response = await HttpSendAsync(deleteRequest);
                    Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
                    Assert.Equal(updatedEtag, response.Headers.ETag);
                }

                // Check that delete with valid etag succeeds
                TestTracer.Trace("==== Check that delete with valid etag succeeds");
                using (HttpRequestMessage deleteRequest = new HttpRequestMessage())
                {
                    deleteRequest.Method = HttpMethod.Delete;
                    deleteRequest.RequestUri = new Uri(fileAddress);
                    deleteRequest.Headers.IfMatch.Add(updatedEtag);
                    response = await HttpSendAsync(deleteRequest);
                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                }

                // Check that 2nd delete attempt fails
                TestTracer.Trace("==== Check that 2nd delete attempt fails");
                using (HttpRequestMessage deleteRequest = new HttpRequestMessage())
                {
                    deleteRequest.Method = HttpMethod.Delete;
                    deleteRequest.RequestUri = new Uri(fileAddress);
                    deleteRequest.Headers.IfMatch.Add(updatedEtag);
                    response = await HttpSendAsync(deleteRequest);
                    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                }

                // Check that we can delete an empty directory
                TestTracer.Trace("==== Check that we can delete an empty directory");
                response = await HttpDeleteAsync(dirAddress);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }
        }

        private async Task<EntityTagHeaderValue> UploadCustomDeploymentScripts()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            string prefix = typeof(VfsControllerBaseTest).Namespace;

            // Upload a custom deploy script sitting outside the repository
            TestTracer.Trace("==== Upload a custom deploy script sitting outside the repository");
            using (Stream resourceStream = assembly.GetManifestResourceStream(prefix + ".Vfs.CustomDeploy.cmd"))
            {
                Uri customDeployment = new Uri(DeploymentBaseAddress + _segmentDelimiter + "/site/customdeploy.cmd");
                HttpRequestMessage request = new HttpRequestMessage();
                request.Method = HttpMethod.Put;
                request.RequestUri = customDeployment;
                request.Headers.IfMatch.Add(EntityTagHeaderValue.Any);
                request.Content = new StreamContent(resourceStream);
                HttpResponseMessage response = await HttpSendAsync(request, DeploymentClient);
                Assert.True(response.IsSuccessStatusCode);
            }

            // Upload a custom .deployment file referencing the custom deploy script
            TestTracer.Trace("==== Upload a custom .deployment file referencing the custom deploy script");
            using (Stream resourceStream = assembly.GetManifestResourceStream(prefix + ".Vfs.DotDeployment.txt"))
            {
                StreamContent content = new StreamContent(resourceStream);
                Uri dotDeployment = new Uri(BaseAddress + _segmentDelimiter + ".deployment");
                HttpResponseMessage response = await HttpPutAsync(dotDeployment.ToString(), content);
                Assert.Equal(HttpStatusCode.Created, response.StatusCode);
                return response.Headers.ETag;
            }
        }

        private async Task<EntityTagHeaderValue> RemoveCustomDeploymentScripts()
        {
            // Delete custom deploy script sitting outside the repository
            TestTracer.Trace("==== Delete custom deploy script sitting outside the repository");
            using (HttpRequestMessage request = new HttpRequestMessage())
            {
                Uri customDeployment = new Uri(DeploymentBaseAddress + _segmentDelimiter + "/site/customdeploy.cmd");
                request.Method = HttpMethod.Delete;
                request.RequestUri = customDeployment;
                request.Headers.IfMatch.Add(EntityTagHeaderValue.Any);
                HttpResponseMessage response = await HttpSendAsync(request, DeploymentClient);
                Assert.True(response.IsSuccessStatusCode);
            }

            // Delete custom .deployment file referencing the custom deploy script
            TestTracer.Trace("==== Delete custom .deployment file referencing the custom deploy script");
            using (HttpRequestMessage request = new HttpRequestMessage())
            {
                Uri dotDeployment = new Uri(BaseAddress + _segmentDelimiter + ".deployment");
                request.Method = HttpMethod.Delete;
                request.RequestUri = dotDeployment;
                request.Headers.IfMatch.Add(EntityTagHeaderValue.Any);
                HttpResponseMessage response = await HttpSendAsync(request);
                Assert.True(response.IsSuccessStatusCode);
                return response.Headers.ETag;
            }
        }

        private async Task VerifyDeployment(string address, HttpStatusCode expectedStatus, byte[] expectedContent)
        {
            if (address != null)
            {
                HttpResponseMessage response = await HttpGetAsync(address, DeploymentClient);
                Assert.Equal(expectedStatus, response.StatusCode);
                if (expectedContent != null)
                {
                    byte[] actualContent = await response.Content.ReadAsByteArrayAsync();
                    Assert.Equal(expectedContent, actualContent);
                }
            }
        }

        protected static HttpContent CreateUploadContent(byte[] content)
        {
            HttpContent uploadContent = new ByteArrayContent(content);
            uploadContent.Headers.ContentType = _fileMediaType;
            return uploadContent;
        }

        private async Task<HttpResponseMessage> HttpGetAsync(string address, HttpClient client = null)
        {
            return await HttpClientRunAsync("Get", address, c => c.GetAsync(address), client);
        }

        protected async Task<HttpResponseMessage> HttpPutAsync(string address, HttpContent content, HttpClient client = null)
        {
            return await HttpClientRunAsync("Put", address, c => c.PutAsync(address, content), client);
        }

        private async Task<HttpResponseMessage> HttpDeleteAsync(string address, HttpClient client = null)
        {
            return await HttpClientRunAsync("Delete", address, c => c.DeleteAsync(address), client);
        }

        private async Task<HttpResponseMessage> HttpSendAsync(HttpRequestMessage httpRequestMessage, HttpClient client = null)
        {
            return await HttpClientRunAsync("Send", httpRequestMessage.RequestUri.ToString(), c => c.SendAsync(httpRequestMessage), client);
        }

        private async Task<HttpResponseMessage> HttpClientRunAsync(string methodName, string address, Func<HttpClient, Task<HttpResponseMessage>> httpClientAction, HttpClient client)
        {
            if (client == null)
            {
                client = Client;
            }

            TestTracer.Trace("HTTP Client {0} - {1}", methodName, address);
            HttpResponseMessage httpResponseMessage = await httpClientAction(client);
            TestTracer.Trace("Response status - {0}", httpResponseMessage.StatusCode);
            return httpResponseMessage;
        }
    }
}