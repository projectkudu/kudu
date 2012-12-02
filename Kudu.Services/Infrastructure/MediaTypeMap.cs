using System;
using System.Collections.Concurrent;
using System.Net.Http.Headers;
using Microsoft.Win32;

namespace Kudu.Services.Infrastructure
{
    /// <summary>
    /// Provides a cache of file name extensions to media type mappings
    /// </summary>
    public class MediaTypeMap
    {
        private readonly MediaTypeHeaderValue _defaultMediaType = MediaTypeHeaderValue.Parse("application/octet-stream");
        private readonly ConcurrentDictionary<string, MediaTypeHeaderValue> _mediatypeMap = new ConcurrentDictionary<string, MediaTypeHeaderValue>(StringComparer.OrdinalIgnoreCase);

        public MediaTypeHeaderValue GetMediaType(string fileExtension)
        {
            if (fileExtension == null)
            {
                throw new ArgumentNullException("fileExtension");
            }

            return _mediatypeMap.GetOrAdd(fileExtension,
                (extension) =>
                {
                    using (RegistryKey key = Registry.ClassesRoot.OpenSubKey(fileExtension))
                    {
                        if (key != null)
                        {
                            string keyValue = key.GetValue("Content Type") as string;
                            MediaTypeHeaderValue mediaType;
                            if (keyValue != null && MediaTypeHeaderValue.TryParse(keyValue, out mediaType))
                            {
                                return mediaType;
                            }
                        }
                        return _defaultMediaType;
                    }
                });
        }
    }
}
