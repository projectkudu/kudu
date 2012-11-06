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
            if (httpResponseMessage.StatusCode == HttpStatusCode.InternalServerError)
            {
                // For 500, we serialize the exception message on the server. 
                HttpExceptionMessage exceptionMessage;
                try
                {
                    exceptionMessage = httpResponseMessage.Content.ReadAsAsync<HttpExceptionMessage>().Result;
                }
                catch (InvalidOperationException ex)
                {
                    // This would happen if the response type is not a Json object.
                    throw new HttpRequestException(httpResponseMessage.Content.ReadAsStringAsync().Result, ex);
                }
                exceptionMessage.StatusCode = httpResponseMessage.StatusCode;
                exceptionMessage.ReasonPhrase = httpResponseMessage.ReasonPhrase;

                throw new HttpUnsuccessfulRequestException(exceptionMessage);
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
