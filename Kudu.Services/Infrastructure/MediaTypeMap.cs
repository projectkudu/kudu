using System;
using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Web;

namespace Kudu.Services.Infrastructure
{
    /// <summary>
    /// Provides a cache of file name extensions to media type mappings
    /// </summary>
    public class MediaTypeMap
    {
        private static readonly MediaTypeMap _defaultInstance = new MediaTypeMap();
        private readonly ConcurrentDictionary<string, MediaTypeHeaderValue> _mediatypeMap = CreateMediaTypeMap();
        private readonly MediaTypeHeaderValue _defaultMediaType = MediaTypeHeaderValue.Parse("application/octet-stream");

        public static MediaTypeMap Default
        {
            get { return _defaultInstance; }
        }

        public MediaTypeHeaderValue GetMediaType(string fileExtension)
        {
            if (fileExtension == null)
            {
                throw new ArgumentNullException("fileExtension");
            }

            return _mediatypeMap.GetOrAdd(fileExtension,
                (extension) =>
                {
                    try
                    {
                        string mediaTypeValue = MimeMapping.GetMimeMapping(fileExtension);
                        MediaTypeHeaderValue mediaType;
                        if (mediaTypeValue != null && MediaTypeHeaderValue.TryParse(mediaTypeValue, out mediaType))
                        {
                            return mediaType;
                        }
                        return _defaultMediaType;
                    }
                    catch
                    {
                        return _defaultMediaType;
                    }
                });
        }

        private static ConcurrentDictionary<string, MediaTypeHeaderValue> CreateMediaTypeMap()
        {
            var dictionary = new ConcurrentDictionary<string, MediaTypeHeaderValue>(StringComparer.OrdinalIgnoreCase);
            dictionary.TryAdd(".js", MediaTypeHeaderValue.Parse("application/javascript"));
            dictionary.TryAdd(".json", MediaTypeHeaderValue.Parse("application/json"));

            // Add media type for markdown
            dictionary.TryAdd(".md", MediaTypeHeaderValue.Parse("text/plain"));

            return dictionary;
        }
    }
}
