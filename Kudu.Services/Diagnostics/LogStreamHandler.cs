using System;
using System.Threading;
using System.Web;
using Kudu.Contracts.Tracing;
using Kudu.Core.Tracing;

namespace Kudu.Services.Performance
{
    public class LogStreamHandler : IHttpAsyncHandler
    {
        private readonly LogStreamManager _manager;
        private readonly ITracer _tracer;

        public LogStreamHandler(LogStreamManager manager, ITracer tracer)
        {
            _manager = manager;
            _tracer = tracer;
        }

        public bool IsReusable
        {
            get { return false; }
        }

        public void ProcessRequest(HttpContext context)
        {
            throw new NotImplementedException();
        }

        public IAsyncResult BeginProcessRequest(HttpContext context, AsyncCallback cb, object extraData)
        {
            using (_tracer.Step("LogStreamHandler.BeginProcessRequest"))
            {
                if (string.Equals(context.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
                {
                    return _manager.BeginProcessRequest(context, cb, extraData);
                }
                else
                {
                    return new CompletedAsyncResult(context, 400, cb, extraData);
                }
            }
        }

        public void EndProcessRequest(IAsyncResult result)
        {
            using (_tracer.Step("LogStreamHandler.EndProcessRequest"))
            {
                if (result is CompletedAsyncResult)
                {
                    CompletedAsyncResult.End(result);
                }
                else
                {
                    _manager.EndProcessRequest(result);
                }
            }
        }

        class CompletedAsyncResult : IAsyncResult
        {
            private HttpContext _context;
            private int _statusCode;
            private ManualResetEvent _waitHandle;

            public CompletedAsyncResult(HttpContext context, int statusCode, AsyncCallback cb, object state)
            {
                _context = context;
                _statusCode = statusCode;
                AsyncState = state;
                if (cb != null)
                {
                    cb(this);
                }
            }

            public static void End(IAsyncResult result)
            {
                CompletedAsyncResult completedResult = (CompletedAsyncResult)result;
                completedResult._context.Response.StatusCode = completedResult._statusCode;
            }

            public object AsyncState
            {
                get;
                private set;
            }

            public WaitHandle AsyncWaitHandle
            {
                get { return _waitHandle ?? (_waitHandle = new ManualResetEvent(true)); }
            }

            public bool CompletedSynchronously
            {
                get { return true; }
            }

            public bool IsCompleted
            {
                get { return true; }
            }
        }
    }
}
