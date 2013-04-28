using System;
using System.Net;
using System.Net.Http;

namespace Kudu.Client
{
    public static class HttpResponseMessageExtensions
    {
        /// <summary>
        /// Determines if the HttpResponse is successful, throws otherwise.
        /// </summary>
        public static HttpResponseMessage EnsureSuccessful(this HttpResponseMessage httpResponseMessage)
        {
            // check whether it is successful status code.   if not, we will attempt to read the content and
            // include it as part of the exception message.
            if (!httpResponseMessage.IsSuccessStatusCode)
            {
                HttpExceptionMessage exceptionMessage = null;
                try
                {
                    exceptionMessage = new HttpExceptionMessage
                    {
                        StatusCode = httpResponseMessage.StatusCode,
                        ReasonPhrase = httpResponseMessage.ReasonPhrase,
                        ExceptionMessage = httpResponseMessage.Content.ReadAsStringAsync().Result,
                        ExceptionType = typeof(HttpRequestException).Name
                    };
                }
                catch (Exception)
                {
                    // ignore error from reading content.
                }

                if (exceptionMessage != null)
                {
                    throw new HttpUnsuccessfulRequestException(exceptionMessage);
                }
            }

            return httpResponseMessage.EnsureSuccessStatusCode();
        }
    }

    public class HttpExceptionMessage
    {
        public HttpStatusCode StatusCode { get; set; }

        public string ReasonPhrase { get; set; }

        public string ExceptionMessage { get; set; }

        public string ExceptionType { get; set; }
    }

    public class HttpUnsuccessfulRequestException : HttpRequestException
    {
        public HttpUnsuccessfulRequestException()
            : this(null)
        {
        }

        public HttpUnsuccessfulRequestException(HttpExceptionMessage responseMessage)
            : base(
                responseMessage != null ?
                    String.Format("{0}: {1}\nStatus Code: {2}", responseMessage.ReasonPhrase, responseMessage.ExceptionMessage, responseMessage.StatusCode) :
                    null)
        {
            ResponseMessage = responseMessage;
        }

        public HttpExceptionMessage ResponseMessage { get; private set; }
    }
}
