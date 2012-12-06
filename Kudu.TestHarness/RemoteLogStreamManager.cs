using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Kudu.Client.Infrastructure;

namespace Kudu.TestHarness
{
    // This is a test class current workaround Stream.Close hangs.
    public class RemoteLogStreamManager : KuduRemoteClientBase
    {
        public RemoteLogStreamManager(string serviceUrl, ICredentials credentials = null)
            : base(serviceUrl, credentials)
        {
        }

        public Task<Stream> GetStream()
        {
            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(ServiceUrl);
            TaskCompletionSource<Stream> tcs = new TaskCompletionSource<Stream>();
            RequestState state = new RequestState { Manager = this, TaskCompletionSource = tcs, Request = request };

            if (Credentials != null)
            {
                NetworkCredential networkCred = Credentials.GetCredential(Client.BaseAddress, "Basic");
                string credParameter = Convert.ToBase64String(Encoding.ASCII.GetBytes(networkCred.UserName + ":" + networkCred.Password));
                request.Headers["Authorization"] = "Basic " + credParameter;
            }

            IAsyncResult result = request.BeginGetResponse(RemoteLogStreamManager.OnGetResponse, state);
            if (result.CompletedSynchronously)
            {
                state.Response = (HttpWebResponse)request.EndGetResponse(result);
                OnGetResponse(state);
            }

            return tcs.Task;
        }

        private static void OnGetResponse(IAsyncResult result)
        {
            RequestState state = (RequestState)result.AsyncState;
            try
            {
                state.Response = (HttpWebResponse)state.Request.EndGetResponse(result);
                state.Manager.OnGetResponse(state);
            }
            catch (Exception ex)
            {
                state.TaskCompletionSource.TrySetException(ex);
            }
        }

        private void OnGetResponse(RequestState state)
        {
            state.TaskCompletionSource.TrySetResult(new DelegatingStream(state.Response.GetResponseStream(), state));
        }

        class RequestState
        {
            public RemoteLogStreamManager Manager { get; set; }
            public TaskCompletionSource<Stream> TaskCompletionSource { get; set; }
            public HttpWebRequest Request { get; set; }
            public HttpWebResponse Response { get; set; }
        }

        class DelegatingStream : Stream
        {
            Stream inner;
            RequestState state;

            public DelegatingStream(Stream inner, RequestState state)
            {
                this.inner = inner;
                this.state = state;
            }

            public override void Close()
            {
                // To avoid hanging!
                this.state.Request.Abort();

                this.inner.Close();
            }

            public override bool CanRead
            {
                get { return this.inner.CanRead; }
            }

            public override bool CanSeek
            {
                get { return this.inner.CanSeek; }
            }

            public override bool CanWrite
            {
                get { return this.inner.CanWrite; }
            }

            public override void Flush()
            {
                this.inner.Flush();
            }

            public override long Length
            {
                get { return this.inner.Length; }
            }

            public override long Position
            {
                get { return this.inner.Position; }
                set { this.inner.Position = value; }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return this.inner.Read(buffer, offset, count);
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                return this.inner.Seek(offset, origin);
            }

            public override void SetLength(long value)
            {
                this.inner.SetLength(value);
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                this.inner.Write(buffer, offset, count);
            }
        }
    }
}
