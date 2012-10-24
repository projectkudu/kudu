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
                var exceptionMessage = httpResponseMessage.Content.ReadAsAsync<HttpExceptionMessage>().Result;
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
                    string.Format("{0}: {1}\nStatus Code: {2}", responseMessage.ReasonPhrase, responseMessage.ExceptionMessage, responseMessage.StatusCode) :
                    null)
        {
            ResponseMessage = responseMessage;
        }

        public HttpExceptionMessage ResponseMessage { get; private set; }
    }
}
