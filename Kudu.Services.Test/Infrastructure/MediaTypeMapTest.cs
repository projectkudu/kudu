using System;
using System.Net.Http.Headers;
using Xunit;

namespace Kudu.Services.Infrastructure.Test
{
    public class MediaTypeMapTest
    {
        [Fact]
        public void GetMediaType_Guards()
        {
            MediaTypeMap map = new MediaTypeMap();
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() => map.GetMediaType(null));
            Assert.Equal("fileExtension", ex.ParamName);
        }

        [Theory]
        [InlineData(".1234")]
        [InlineData(".//")]
        [InlineData(".1  4")]
        [InlineData(".foo")]
        public void GetMediaType_HandlesUnknownMediaTypes(string extension)
        {
            MediaTypeMap map = new MediaTypeMap();
            MediaTypeHeaderValue expectedMediaType = new MediaTypeHeaderValue("application/octet-stream");

            Assert.Equal(expectedMediaType, map.GetMediaType(extension));
        }

        [Theory]
        [InlineData(".txt", "text/plain")]
        [InlineData(".html", "text/html")]
        [InlineData(".js", "application/javascript")]
        [InlineData(".json", "application/json")]
        [InlineData(".md", "text/plain")]
        public void GetMediaType_HandlesKnownMediaTypes(string extension, string expectedMediaType)
        {
            MediaTypeMap map = new MediaTypeMap();
            MediaTypeHeaderValue expected = new MediaTypeHeaderValue(expectedMediaType);
            Assert.Equal(expected, map.GetMediaType(extension));
        }
    }
}
