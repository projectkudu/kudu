using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
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

        public VfsControllerBaseTest(RemoteVfsManager client, bool testConflictingUpdates)
        {
            KuduClient = client;
            Client = client.Client;
            BaseAddress = Client.BaseAddress.GetComponents(UriComponents.HttpRequestUrl, UriFormat.Unescaped).TrimEnd(_segmentDelimiters);
            _testConflictingUpdates = testConflictingUpdates;
        }

        protected string BaseAddress { get; private set; }

        protected RemoteVfsManager KuduClient { get; private set; }

        protected HttpClient Client { get; private set; }

        public void RunIntegrationTest()
        {
            string dir = Guid.NewGuid().ToString("N");
            string dirAddress = BaseAddress + _segmentDelimiter + dir;
            string dirAddressWithTerminatingSlash = dirAddress + _segmentDelimiter;

            string file = Guid.NewGuid().ToString("N") + ".txt";
            string fileAddress = dirAddressWithTerminatingSlash + file;
            string fileAddressWithTerminatingSlash = fileAddress + _segmentDelimiter;

            HttpResponseMessage response;

            // Check not found file responses
            response = Client.GetAsync(dirAddress).Result;
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

            response = Client.GetAsync(dirAddressWithTerminatingSlash).Result;
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

            response = Client.GetAsync(fileAddress).Result;
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

            response = Client.GetAsync(fileAddressWithTerminatingSlash).Result;
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

            // Check create file results in 201 response with etag
            response = Client.PutAsync(fileAddress, CreateUploadContent(_fileContent0)).Result;
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            EntityTagHeaderValue originalEtag = response.Headers.ETag;
            Assert.NotNull(originalEtag);

            // Check that we get a 200 (OK) on created file with the correct etag
            response = Client.GetAsync(fileAddress).Result;
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(originalEtag, response.Headers.ETag);
            Assert.Equal(_fileMediaType, response.Content.Headers.ContentType);

            // Check that we get a 200 (OK) on created file using HEAD with the correct etag
            using (HttpRequestMessage headReq = new HttpRequestMessage())
            {
                headReq.Method = HttpMethod.Head;
                headReq.RequestUri = new Uri(fileAddress);
                response = Client.SendAsync(headReq).Result;
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Equal(originalEtag, response.Headers.ETag);
                Assert.Equal(_fileMediaType, response.Content.Headers.ContentType);
            }

            // Check that we get a 304 (Not Modified) response if matching If-None-Match
            using (HttpRequestMessage ifNoneMatchReq = new HttpRequestMessage())
            {
                ifNoneMatchReq.RequestUri = new Uri(fileAddress);
                ifNoneMatchReq.Headers.IfNoneMatch.Add(originalEtag);
                response = Client.SendAsync(ifNoneMatchReq).Result;
                Assert.Equal(HttpStatusCode.NotModified, response.StatusCode);
                Assert.Equal(originalEtag, response.Headers.ETag);
            }

            // Check that we get a 200 (OK) response if not matching If-None-Match
            using (HttpRequestMessage ifNoneMatchReqBadEtag = new HttpRequestMessage())
            {
                ifNoneMatchReqBadEtag.RequestUri = new Uri(fileAddress);
                ifNoneMatchReqBadEtag.Headers.IfNoneMatch.Add(new EntityTagHeaderValue("\"NotMatching\""));
                response = Client.SendAsync(ifNoneMatchReqBadEtag).Result;
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Equal(originalEtag, response.Headers.ETag);
            }

            // Check that If-Range request with range returns 206 (Partial Content)
            using (HttpRequestMessage ifRangeReq = new HttpRequestMessage())
            {
                ifRangeReq.RequestUri = new Uri(fileAddress);
                ifRangeReq.Headers.IfRange = new RangeConditionHeaderValue(originalEtag);
                ifRangeReq.Headers.Range = new RangeHeaderValue(0, 0) { Unit = "bytes" };
                response = Client.SendAsync(ifRangeReq).Result;
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
                response = Client.SendAsync(ifRangeReqNoRange).Result;
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
                response = Client.SendAsync(ifRangeReqBadRange).Result;
                Assert.Equal(HttpStatusCode.RequestedRangeNotSatisfiable, response.StatusCode);
                Assert.Equal(_fileContentRange, response.Content.Headers.ContentRange);
            }

            // Check that we get a root directory view
            response = Client.GetAsync(BaseAddress).Result;
            Assert.Equal(_dirMediaType, response.Content.Headers.ContentType);

            // Check that we get a directory view from folder
            response = Client.GetAsync(dirAddress).Result;
            Assert.Equal(_dirMediaType, response.Content.Headers.ContentType);

            // Check various redirects between files and folders
            HttpClientHandler redirectHandler = HttpClientHelper.CreateClientHandler(BaseAddress, KuduClient.Credentials);
            redirectHandler.AllowAutoRedirect = false;
            using (HttpClient redirectClient = HttpClientHelper.CreateClient(BaseAddress, KuduClient.Credentials, redirectHandler))
            {
                // Ensure that requests to root without slash is redirected to one with slash
                response = redirectClient.GetAsync(BaseAddress).Result;
                Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);
                Assert.Equal(BaseAddress + _segmentDelimiter, response.Headers.Location.AbsoluteUri);

                // Ensure that requests to directory without slash is redirected to one with slash
                response = redirectClient.GetAsync(dirAddress).Result;
                Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);
                Assert.Equal(dirAddressWithTerminatingSlash, response.Headers.Location.AbsoluteUri);

                // Ensure that requests to file with slash is redirected to one without slash
                response = redirectClient.GetAsync(fileAddressWithTerminatingSlash).Result;
                Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);
                Assert.Equal(fileAddress, response.Headers.Location.AbsoluteUri);
            }

            // Check that 2nd create attempt fails
            response = Client.PutAsync(fileAddress, CreateUploadContent(_fileContent0)).Result;
            Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
            Assert.Equal(originalEtag, response.Headers.ETag);

            // Check that we can't update a directory
            response = Client.PutAsync(dirAddress, CreateUploadContent(_fileContent0)).Result;
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

            // Check that we can't delete a non-empty directory
            response = Client.DeleteAsync(dirAddress).Result;
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
                    response = Client.SendAsync(update1).Result;
                    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
                    Assert.NotNull(response.Headers.ETag);
                    Assert.NotEqual(originalEtag, response.Headers.ETag);
                    updatedEtag = response.Headers.ETag;
                }

                // Update file with second edit based on original etag (non-conflicting)
                using (HttpRequestMessage update2 = new HttpRequestMessage())
                {
                    update2.Method = HttpMethod.Put;
                    update2.RequestUri = new Uri(fileAddress);
                    update2.Headers.IfMatch.Add(originalEtag);
                    update2.Content = CreateUploadContent(_fileContent2);
                    response = Client.SendAsync(update2).Result;
                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                    Assert.NotNull(response.Headers.ETag);
                    Assert.NotEqual(updatedEtag, response.Headers.ETag);
                    Assert.Equal(_fileMediaType, response.Content.Headers.ContentType);
                    updatedEtag = response.Headers.ETag;
                }

                // Update file with third edit based on original etag (non-conflicting)
                using (HttpRequestMessage update3 = new HttpRequestMessage())
                {
                    update3.Method = HttpMethod.Put;
                    update3.RequestUri = new Uri(fileAddress);
                    update3.Headers.IfMatch.Add(originalEtag);
                    update3.Content = CreateUploadContent(_fileContent3);
                    response = Client.SendAsync(update3).Result;
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
                    response = Client.SendAsync(update4).Result;
                    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
                    Assert.Equal(_conflictMediaType, response.Content.Headers.ContentType);
                    Assert.Null(response.Headers.ETag);
                    string content = response.Content.ReadAsStringAsync().Result;
                    Assert.True(content.StartsWith(_conflict));
                }

                // Update file with fifth edit based on invalid etag
                using (HttpRequestMessage update5 = new HttpRequestMessage())
                {
                    update5.Method = HttpMethod.Put;
                    update5.RequestUri = new Uri(fileAddress);
                    update5.Headers.IfMatch.Add(new EntityTagHeaderValue("\"invalidetag\""));
                    update5.Content = CreateUploadContent(_fileContent1);
                    response = Client.SendAsync(update5).Result;
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
                    response = Client.SendAsync(update6).Result;
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
                    response = Client.SendAsync(deleteRequest).Result;
                    Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
                    Assert.Equal(updatedEtag, response.Headers.ETag);
                }

                // Check that delete with conflict fails
                using (HttpRequestMessage deleteRequest = new HttpRequestMessage())
                {
                    deleteRequest.Method = HttpMethod.Delete;
                    deleteRequest.RequestUri = new Uri(fileAddress);
                    deleteRequest.Headers.IfMatch.Add(originalEtag);
                    response = Client.SendAsync(deleteRequest).Result;
                    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
                }

                // Check that delete with valid etag succeeds
                using (HttpRequestMessage deleteRequest = new HttpRequestMessage())
                {
                    deleteRequest.Method = HttpMethod.Delete;
                    deleteRequest.RequestUri = new Uri(fileAddress);
                    deleteRequest.Headers.IfMatch.Add(updatedEtag);
                    response = Client.SendAsync(deleteRequest).Result;
                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                }

                // Check that 2nd delete attempt fails
                using (HttpRequestMessage deleteRequest = new HttpRequestMessage())
                {
                    deleteRequest.Method = HttpMethod.Delete;
                    deleteRequest.RequestUri = new Uri(fileAddress);
                    deleteRequest.Headers.IfMatch.Add(updatedEtag);
                    response = Client.SendAsync(deleteRequest).Result;
                    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                }

                // Check that we can delete an empty directory
                response = Client.DeleteAsync(dirAddress).Result;
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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
                    response = Client.SendAsync(updateRequest).Result;
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
                    response = Client.SendAsync(updateRequest).Result;
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
                    response = Client.SendAsync(updateRequest).Result;
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
                    response = Client.SendAsync(updateRequest).Result;
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
                    response = Client.SendAsync(deleteRequest).Result;
                    Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
                    Assert.Equal(updatedEtag, response.Headers.ETag);
                }

                // Check that delete with valid etag succeeds
                using (HttpRequestMessage deleteRequest = new HttpRequestMessage())
                {
                    deleteRequest.Method = HttpMethod.Delete;
                    deleteRequest.RequestUri = new Uri(fileAddress);
                    deleteRequest.Headers.IfMatch.Add(updatedEtag);
                    response = Client.SendAsync(deleteRequest).Result;
                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                }

                // Check that 2nd delete attempt fails
                using (HttpRequestMessage deleteRequest = new HttpRequestMessage())
                {
                    deleteRequest.Method = HttpMethod.Delete;
                    deleteRequest.RequestUri = new Uri(fileAddress);
                    deleteRequest.Headers.IfMatch.Add(updatedEtag);
                    response = Client.SendAsync(deleteRequest).Result;
                    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                }

                // Check that we can delete an empty directory
                response = Client.DeleteAsync(dirAddress).Result;
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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
