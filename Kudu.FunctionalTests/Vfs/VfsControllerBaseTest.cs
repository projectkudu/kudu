using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Kudu.Client.Editor;
using Kudu.Client.Infrastructure;
using Xunit;

namespace Kudu.FunctionalTests
{
    public class VfsControllerBaseTest
    {
        private static readonly char _segmentDelimiter = '/';
        private static readonly char[] _segmentDelimiters = new char[] { _segmentDelimiter };

        private static readonly byte[] _fileContent0 = Encoding.UTF8.GetBytes("aaa\r\nbbb\r\nccc\r\n");
        private static readonly byte[] _fileContent1 = Encoding.UTF8.GetBytes("AAA\r\nbbb\r\nccc\r\n");
        private static readonly byte[] _fileContent2 = Encoding.UTF8.GetBytes("aaa\r\nbbb\r\nCCC\r\n");
        private static readonly byte[] _fileContent3 = Encoding.UTF8.GetBytes("AAA\r\nbbb\r\nCCC\r\n");
        private static readonly byte[] _fileContent4 = Encoding.UTF8.GetBytes("CCC\r\nbbb\r\nAAA\r\n");

        private static readonly ContentRangeHeaderValue _fileContentRange = new ContentRangeHeaderValue(_fileContent0.Length);

        private static readonly string _conflict = "<<<<<<< HEAD\r\nAAA\r\nbbb\r\nCCC\r\n=======\r\nCCC\r\nbbb\r\nAAA\r\n>>>>>>>";

        private static readonly MediaTypeHeaderValue _fileMediaType = MediaTypeHeaderValue.Parse("text/plain");
        private static readonly MediaTypeHeaderValue _dirMediaType = MediaTypeHeaderValue.Parse("application/json; charset=utf-8");
        private static readonly MediaTypeHeaderValue _conflictMediaType = MediaTypeHeaderValue.Parse("text/plain");

        private bool _testConflictingUpdates;

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
        }

        protected string BaseAddress { get; private set; }

        protected RemoteVfsManager KuduClient { get; private set; }

        protected HttpClient Client { get; private set; }

        protected HttpClient DeploymentClient { get; private set; }

        protected string DeploymentBaseAddress { get; private set; }

        public async Task RunIntegrationTest()
        {
            string dir = Guid.NewGuid().ToString("N");
            string dirAddress = BaseAddress + _segmentDelimiter + dir;
            string dirAddressWithTerminatingSlash = dirAddress + _segmentDelimiter;

            string file = Guid.NewGuid().ToString("N") + ".txt";
            string fileAddress = dirAddressWithTerminatingSlash + file;
            string fileAddressWithTerminatingSlash = fileAddress + _segmentDelimiter;

            string deploymentFileAddress = DeploymentClient != null ? DeploymentBaseAddress + _segmentDelimiter + dir + _segmentDelimiter + file : null;

            HttpResponseMessage response;

            // Check not found file responses
            response = await Client.GetAsync(dirAddress);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

            response = await Client.GetAsync(dirAddressWithTerminatingSlash);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

            response = await Client.GetAsync(fileAddress);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

            response = await Client.GetAsync(fileAddressWithTerminatingSlash);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

            // Check create file results in 201 response with etag
            response = await Client.PutAsync(fileAddress, CreateUploadContent(_fileContent0));
            await VerifyDeployment(deploymentFileAddress, HttpStatusCode.OK, _fileContent0);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            EntityTagHeaderValue originalEtag = response.Headers.ETag;
            Assert.NotNull(originalEtag);

            // Check that we get a 200 (OK) on created file with the correct etag
            response = await Client.GetAsync(fileAddress);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(originalEtag, response.Headers.ETag);
            Assert.Equal(_fileMediaType, response.Content.Headers.ContentType);

            // Check that we get a 200 (OK) on created file using HEAD with the correct etag
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
            using (HttpRequestMessage ifNoneMatchReq = new HttpRequestMessage())
            {
                ifNoneMatchReq.RequestUri = new Uri(fileAddress);
                ifNoneMatchReq.Headers.IfNoneMatch.Add(originalEtag);
                response = await Client.SendAsync(ifNoneMatchReq);
                Assert.Equal(HttpStatusCode.NotModified, response.StatusCode);
                Assert.Equal(originalEtag, response.Headers.ETag);
            }

            // Check that we get a 200 (OK) response if not matching If-None-Match
            using (HttpRequestMessage ifNoneMatchReqBadEtag = new HttpRequestMessage())
            {
                ifNoneMatchReqBadEtag.RequestUri = new Uri(fileAddress);
                ifNoneMatchReqBadEtag.Headers.IfNoneMatch.Add(new EntityTagHeaderValue("\"NotMatching\""));
                response = await Client.SendAsync(ifNoneMatchReqBadEtag);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Equal(originalEtag, response.Headers.ETag);
            }

            // Check that If-Range request with range returns 206 (Partial Content)
            using (HttpRequestMessage ifRangeReq = new HttpRequestMessage())
            {
                ifRangeReq.RequestUri = new Uri(fileAddress);
                ifRangeReq.Headers.IfRange = new RangeConditionHeaderValue(originalEtag);
                ifRangeReq.Headers.Range = new RangeHeaderValue(0, 0) { Unit = "bytes" };
                response = await Client.SendAsync(ifRangeReq);
                Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);
                Assert.Equal(originalEtag, response.Headers.ETag);
                Assert.Equal(1, response.Content.Headers.ContentLength);
                Assert.Equal(new ContentRangeHeaderValue(0, 0, _fileContent0.Length), response.Content.Headers.ContentRange);
            }

            // Check that If-Range request with no range returns 200 (OK)
            using (HttpRequestMessage ifRangeReqNoRange = new HttpRequestMessage())
            {
                ifRangeReqNoRange.RequestUri = new Uri(fileAddress);
                ifRangeReqNoRange.Headers.IfRange = new RangeConditionHeaderValue(originalEtag);
                response = await Client.SendAsync(ifRangeReqNoRange);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Equal(originalEtag, response.Headers.ETag);
            }

            // Check that If-Range request with bad range returns 416 (Requested Range Not Satisfiable) 
            // including a Content-Range header
            using (HttpRequestMessage ifRangeReqBadRange = new HttpRequestMessage())
            {
                ifRangeReqBadRange.RequestUri = new Uri(fileAddress);
                ifRangeReqBadRange.Headers.IfRange = new RangeConditionHeaderValue(originalEtag);
                ifRangeReqBadRange.Headers.Range = new RangeHeaderValue(100, 100) { Unit = "bytes" };
                response = await Client.SendAsync(ifRangeReqBadRange);
                Assert.Equal(HttpStatusCode.RequestedRangeNotSatisfiable, response.StatusCode);
                Assert.Equal(_fileContentRange, response.Content.Headers.ContentRange);
            }

            // Check that we get a root directory view
            response = await Client.GetAsync(BaseAddress);
            Assert.Equal(_dirMediaType, response.Content.Headers.ContentType);

            // Check that we get a directory view from folder
            response = await Client.GetAsync(dirAddress);
            Assert.Equal(_dirMediaType, response.Content.Headers.ContentType);

            // Check various redirects between files and folders
            HttpClientHandler redirectHandler = HttpClientHelper.CreateClientHandler(BaseAddress, KuduClient.Credentials);
            redirectHandler.AllowAutoRedirect = false;
            using (HttpClient redirectClient = HttpClientHelper.CreateClient(BaseAddress, KuduClient.Credentials, redirectHandler))
            {
                // Ensure that requests to root without slash is redirected to one with slash
                response = await redirectClient.GetAsync(BaseAddress);
                Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);
                Assert.Equal(BaseAddress + _segmentDelimiter, response.Headers.Location.AbsoluteUri);

                // Ensure that requests to directory without slash is redirected to one with slash
                response = await redirectClient.GetAsync(dirAddress);
                Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);
                Assert.Equal(dirAddressWithTerminatingSlash, response.Headers.Location.AbsoluteUri);

                // Ensure that requests to file with slash is redirected to one without slash
                response = await redirectClient.GetAsync(fileAddressWithTerminatingSlash);
                Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);
                Assert.Equal(fileAddress, response.Headers.Location.AbsoluteUri);
            }

            // Check that 2nd create attempt fails
            response = await Client.PutAsync(fileAddress, CreateUploadContent(_fileContent0));
            Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
            Assert.Equal(originalEtag, response.Headers.ETag);

            // Check that we can't update a directory
            response = await Client.PutAsync(dirAddress, CreateUploadContent(_fileContent0));
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

            // Check that we can't delete a non-empty directory
            response = await Client.DeleteAsync(dirAddress);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

            EntityTagHeaderValue updatedEtag;
            if (_testConflictingUpdates)
            {
                // Update file with first edit based on original etag
                using (HttpRequestMessage update1 = new HttpRequestMessage())
                {
                    update1.Method = HttpMethod.Put;
                    update1.RequestUri = new Uri(fileAddress);
                    update1.Headers.IfMatch.Add(originalEtag);
                    update1.Content = CreateUploadContent(_fileContent1);

                    response = await Client.SendAsync(update1);
                    await VerifyDeployment(deploymentFileAddress, HttpStatusCode.OK, _fileContent1);

                    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
                    Assert.NotNull(response.Headers.ETag);
                    Assert.NotEqual(originalEtag, response.Headers.ETag);
                    updatedEtag = response.Headers.ETag;
                }

                // Update file with second edit based on original etag (non-conflicting merge)
                using (HttpRequestMessage update2 = new HttpRequestMessage())
                {
                    update2.Method = HttpMethod.Put;
                    update2.RequestUri = new Uri(fileAddress);
                    update2.Headers.IfMatch.Add(originalEtag);
                    update2.Content = CreateUploadContent(_fileContent2);

                    response = await Client.SendAsync(update2);
                    await VerifyDeployment(deploymentFileAddress, HttpStatusCode.OK, _fileContent3);

                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                    Assert.NotNull(response.Headers.ETag);
                    Assert.NotEqual(updatedEtag, response.Headers.ETag);
                    Assert.Equal(_fileMediaType, response.Content.Headers.ContentType);
                    updatedEtag = response.Headers.ETag;
                }

                // Update file with third edit based on original etag (non-conflicting merge)
                using (HttpRequestMessage update3 = new HttpRequestMessage())
                {
                    update3.Method = HttpMethod.Put;
                    update3.RequestUri = new Uri(fileAddress);
                    update3.Headers.IfMatch.Add(originalEtag);
                    update3.Content = CreateUploadContent(_fileContent3);

                    response = await Client.SendAsync(update3);
                    await VerifyDeployment(deploymentFileAddress, HttpStatusCode.OK, _fileContent3);

                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                    Assert.NotNull(response.Headers.ETag);
                    Assert.Equal(updatedEtag, response.Headers.ETag);
                    Assert.Equal(_fileMediaType, response.Content.Headers.ContentType);
                    updatedEtag = response.Headers.ETag;
                }

                // Update file with forth edit based on original etag (conflicting)
                using (HttpRequestMessage update4 = new HttpRequestMessage())
                {
                    update4.Method = HttpMethod.Put;
                    update4.RequestUri = new Uri(fileAddress);
                    update4.Headers.IfMatch.Add(originalEtag);
                    update4.Content = CreateUploadContent(_fileContent4);

                    response = await Client.SendAsync(update4);
                    await VerifyDeployment(deploymentFileAddress, HttpStatusCode.OK, _fileContent3);

                    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
                    Assert.Equal(_conflictMediaType, response.Content.Headers.ContentType);
                    Assert.Null(response.Headers.ETag);
                    string content = await response.Content.ReadAsStringAsync();
                    Assert.True(content.StartsWith(_conflict));
                }

                // The previous conflict results in a git cleanup which at times takes time. During this interval the server responds with ServerUnavailable. 
                // To work aroudn this, we'll simply add a bit of sleep timing. 
                Thread.Sleep(TimeSpan.FromSeconds(3));

                // Update file with fifth edit based on invalid etag
                using (HttpRequestMessage update5 = new HttpRequestMessage())
                {
                    update5.Method = HttpMethod.Put;
                    update5.RequestUri = new Uri(fileAddress);
                    update5.Headers.IfMatch.Add(new EntityTagHeaderValue("\"invalidetag\""));
                    update5.Content = CreateUploadContent(_fileContent1);

                    response = await Client.SendAsync(update5);
                    await VerifyDeployment(deploymentFileAddress, HttpStatusCode.OK, _fileContent3);

                    Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
                    Assert.Equal(updatedEtag, response.Headers.ETag);
                }

                // Check that update with wildcard etag succeeds
                using (HttpRequestMessage update6 = new HttpRequestMessage())
                {
                    update6.Method = HttpMethod.Put;
                    update6.RequestUri = new Uri(fileAddress);
                    update6.Headers.IfMatch.Add(EntityTagHeaderValue.Any);
                    update6.Content = CreateUploadContent(_fileContent1);

                    response = await Client.SendAsync(update6);
                    await VerifyDeployment(deploymentFileAddress, HttpStatusCode.OK, _fileContent1);

                    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
                    Assert.NotNull(response.Headers.ETag);
                    Assert.NotEqual(originalEtag, response.Headers.ETag);
                    updatedEtag = response.Headers.ETag;
                }

                // Check that delete with invalid etag fails
                using (HttpRequestMessage deleteRequest = new HttpRequestMessage())
                {
                    deleteRequest.Method = HttpMethod.Delete;
                    deleteRequest.RequestUri = new Uri(fileAddress);
                    deleteRequest.Headers.IfMatch.Add(new EntityTagHeaderValue("\"invalidetag\""));

                    response = await Client.SendAsync(deleteRequest);
                    await VerifyDeployment(deploymentFileAddress, HttpStatusCode.OK, _fileContent1);

                    Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
                    Assert.Equal(updatedEtag, response.Headers.ETag);
                }

                // Check that delete with conflict fails
                using (HttpRequestMessage deleteRequest = new HttpRequestMessage())
                {
                    deleteRequest.Method = HttpMethod.Delete;
                    deleteRequest.RequestUri = new Uri(fileAddress);
                    deleteRequest.Headers.IfMatch.Add(originalEtag);

                    response = await Client.SendAsync(deleteRequest);
                    await VerifyDeployment(deploymentFileAddress, HttpStatusCode.OK, _fileContent1);

                    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
                }

                // Check that delete with valid etag succeeds
                using (HttpRequestMessage deleteRequest = new HttpRequestMessage())
                {
                    deleteRequest.Method = HttpMethod.Delete;
                    deleteRequest.RequestUri = new Uri(fileAddress);
                    deleteRequest.Headers.IfMatch.Add(updatedEtag);

                    response = await Client.SendAsync(deleteRequest);
                    await VerifyDeployment(deploymentFileAddress, HttpStatusCode.NotFound, null);

                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                }

                // Check that 2nd delete attempt fails
                using (HttpRequestMessage deleteRequest = new HttpRequestMessage())
                {
                    deleteRequest.Method = HttpMethod.Delete;
                    deleteRequest.RequestUri = new Uri(fileAddress);
                    deleteRequest.Headers.IfMatch.Add(updatedEtag);

                    response = await Client.SendAsync(deleteRequest);
                    await VerifyDeployment(deploymentFileAddress, HttpStatusCode.NotFound, null);

                    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                }
            }
            else
            {
                // Check that update with correct etag generates 204 Response with new etag
                using (HttpRequestMessage updateRequest = new HttpRequestMessage())
                {
                    updateRequest.Method = HttpMethod.Put;
                    updateRequest.RequestUri = new Uri(fileAddress);
                    updateRequest.Headers.IfMatch.Add(originalEtag);
                    updateRequest.Content = CreateUploadContent(_fileContent1);
                    response = await Client.SendAsync(updateRequest);
                    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
                    Assert.NotNull(response.Headers.ETag);
                    Assert.NotEqual(originalEtag, response.Headers.ETag);
                    updatedEtag = response.Headers.ETag;
                }

                // Check that 2nd create attempt fails
                using (HttpRequestMessage updateRequest = new HttpRequestMessage())
                {
                    updateRequest.Method = HttpMethod.Put;
                    updateRequest.RequestUri = new Uri(fileAddress);
                    updateRequest.Headers.IfMatch.Add(originalEtag);
                    updateRequest.Content = CreateUploadContent(_fileContent2);
                    response = await Client.SendAsync(updateRequest);
                    Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
                    Assert.Equal(updatedEtag, response.Headers.ETag);
                }

                // Check that update with invalid etag fails
                using (HttpRequestMessage updateRequest = new HttpRequestMessage())
                {
                    updateRequest.Method = HttpMethod.Put;
                    updateRequest.RequestUri = new Uri(fileAddress);
                    updateRequest.Headers.IfMatch.Add(new EntityTagHeaderValue("\"invalidetag\""));
                    updateRequest.Content = CreateUploadContent(_fileContent1);
                    response = await Client.SendAsync(updateRequest);
                    Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
                    Assert.Equal(updatedEtag, response.Headers.ETag);
                }

                // Check that update with wildcard etag succeeds
                using (HttpRequestMessage updateRequest = new HttpRequestMessage())
                {
                    updateRequest.Method = HttpMethod.Put;
                    updateRequest.RequestUri = new Uri(fileAddress);
                    updateRequest.Headers.IfMatch.Add(EntityTagHeaderValue.Any);
                    updateRequest.Content = CreateUploadContent(_fileContent1);
                    response = await Client.SendAsync(updateRequest);
                    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
                    Assert.NotNull(response.Headers.ETag);
                    Assert.NotEqual(originalEtag, response.Headers.ETag);
                    updatedEtag = response.Headers.ETag;
                }

                // Check that delete with invalid etag fails
                using (HttpRequestMessage deleteRequest = new HttpRequestMessage())
                {
                    deleteRequest.Method = HttpMethod.Delete;
                    deleteRequest.RequestUri = new Uri(fileAddress);
                    deleteRequest.Headers.IfMatch.Add(new EntityTagHeaderValue("\"invalidetag\""));
                    response = await Client.SendAsync(deleteRequest);
                    Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
                    Assert.Equal(updatedEtag, response.Headers.ETag);
                }

                // Check that delete with valid etag succeeds
                using (HttpRequestMessage deleteRequest = new HttpRequestMessage())
                {
                    deleteRequest.Method = HttpMethod.Delete;
                    deleteRequest.RequestUri = new Uri(fileAddress);
                    deleteRequest.Headers.IfMatch.Add(updatedEtag);
                    response = await Client.SendAsync(deleteRequest);
                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                }

                // Check that 2nd delete attempt fails
                using (HttpRequestMessage deleteRequest = new HttpRequestMessage())
                {
                    deleteRequest.Method = HttpMethod.Delete;
                    deleteRequest.RequestUri = new Uri(fileAddress);
                    deleteRequest.Headers.IfMatch.Add(updatedEtag);
                    response = await Client.SendAsync(deleteRequest);
                    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                }

                // Check that we can delete an empty directory
                response = await Client.DeleteAsync(dirAddress);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }
        }

        private async Task VerifyDeployment(string address, HttpStatusCode expectedStatus, byte[] expectedContent)
        {
            if (address != null)
            {
                HttpResponseMessage response = await DeploymentClient.GetAsync(address);
                Assert.Equal(expectedStatus, response.StatusCode);
                if (expectedContent != null)
                {
                    byte[] actualContent = await response.Content.ReadAsByteArrayAsync();
                    Assert.Equal(expectedContent, actualContent);
                }
            }
        }

        private static HttpContent CreateUploadContent(byte[] content)
        {
            HttpContent uploadContent = new ByteArrayContent(content);
            uploadContent.Headers.ContentType = _fileMediaType;
            return uploadContent;
        }
    }
}
