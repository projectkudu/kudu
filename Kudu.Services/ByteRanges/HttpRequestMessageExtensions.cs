using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;

namespace Kudu.Services.ByteRanges
{
    /// <summary>
    /// Provides extension methods for the <see cref="HttpRequestMessage"/> class.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static class HttpRequestMessageExtensions
    {
        /// <summary>
        /// Helper method for creating an <see cref="HttpResponseMessage"/> message with a "416 (Requested Range Not Satisfiable)" status code.
        /// This response can be used in combination with the <see cref="ByteRangeStreamContent"/> to indicate that the requested range or
        /// ranges do not overlap with the current resource. The response contains a "Content-Range" header indicating the valid upper and lower
        /// bounds for requested ranges.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="invalidByteRangeException">An <see cref="InvalidByteRangeException"/> instance, typically thrown by a 
        /// <see cref="ByteRangeStreamContent"/> instance.</param>
        /// <returns>An 416 (Requested Range Not Satisfiable) error response with a Content-Range header indicating the valid range.</returns>
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Satisfiable", Justification = "Word is correctly spelled.")]
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "Caller will dispose")]
        public static HttpResponseMessage CreateErrorResponse(this HttpRequestMessage request, InvalidByteRangeException invalidByteRangeException)
        {
            if (invalidByteRangeException == null)
            {
                throw new ArgumentNullException("invalidByteRangeException");
            }

            HttpResponseMessage rangeNotSatisfiableResponse = request.CreateErrorResponse(HttpStatusCode.RequestedRangeNotSatisfiable, invalidByteRangeException);
            rangeNotSatisfiableResponse.Content.Headers.ContentRange = invalidByteRangeException.ContentRange;
            return rangeNotSatisfiableResponse;
        }
    }
}
