using System;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Runtime.Serialization;

namespace Kudu.Services.ByteRanges
{
    /// <summary>
    /// An exception thrown by <see cref="ByteRangeStreamContent"/> in case none of the requested ranges 
    /// overlap with the current extend of the selected resource. The current extend of the resource
    /// is indicated in the ContentRange property.
    /// </summary>
    [SuppressMessage("Microsoft.Usage", "CA2237:MarkISerializableTypesWithSerializable", Justification = "Exception is not intended to be serialized.")]
    [SuppressMessage("Microsoft.Usage", "CA2240:ImplementISerializableCorrectly", Justification = "Exception is not intended to be serialized.")]
    [SuppressMessage("Microsoft.Design", "CA1032:ImplementStandardExceptionConstructors", Justification = "The ContentRange is a required parameter.")]
    internal class InvalidByteRangeException : Exception
    {
        public InvalidByteRangeException(ContentRangeHeaderValue contentRange)
        {
            Initialize(contentRange);
        }

        public InvalidByteRangeException(ContentRangeHeaderValue contentRange, string message)
            : base(message)
        {
            Initialize(contentRange);
        }

        public InvalidByteRangeException(ContentRangeHeaderValue contentRange, string message, Exception innerException)
            : base(message, innerException)
        {
            Initialize(contentRange);
        }

        public InvalidByteRangeException(ContentRangeHeaderValue contentRange, SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            Initialize(contentRange);
        }

        /// <summary>
        /// The current extend of the resource indicated in terms of a ContentRange header field.
        /// </summary>
        public ContentRangeHeaderValue ContentRange { get; private set; }

        private void Initialize(ContentRangeHeaderValue contentRange)
        {
            if (contentRange == null)
            {
                throw new ArgumentNullException("contentRange");
            }
            ContentRange = contentRange;
        }
    }
}
