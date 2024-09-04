using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;

namespace Kudu.Core.Infrastructure
{
    public static class FileSystemCache
    {
        private readonly static ConcurrentDictionary<string, FileContentCache> _fileContentCaches 
            = new ConcurrentDictionary<string, FileContentCache>(StringComparer.OrdinalIgnoreCase);

        public static string ReadAllText(string path)
        {
            var lastWriteTimeUtc = FileSystemHelpers.GetLastWriteTimeUtc(path);
            if (!_fileContentCaches.TryGetValue(path, out FileContentCache cache)
                || cache.LastWriteTimeUtc < lastWriteTimeUtc)
            {
                cache = new FileContentCache(path, lastWriteTimeUtc);
                _fileContentCaches.AddOrUpdate(path, cache, (_, __) => cache);
            }

            return cache.Content;
        }

        public static XDocument ReadXml(string path)
        {
            var lastWriteTimeUtc = FileSystemHelpers.GetLastWriteTimeUtc(path);
            if (!_fileContentCaches.TryGetValue(path, out FileContentCache cache)
                || cache.LastWriteTimeUtc < lastWriteTimeUtc)
            {
                cache = new FileContentCache(path, lastWriteTimeUtc, isXml: true);
                _fileContentCaches.AddOrUpdate(path, cache, (_, __) => cache);
            }

            return cache.Xml;
        }

        public static void CleanUp()
        {
            const int MaxFileContentCache = 50;
            if (_fileContentCaches.Count > MaxFileContentCache)
            {
                foreach (var cache in _fileContentCaches.Values.OrderBy(f => f.LastAccessTimeUtc).Take(10))
                {
                    _fileContentCaches.TryRemove(cache.Path, out _);
                }
            }
        }

        class FileContentCache
        {
            private readonly string _path;
            private readonly DateTime _lastWriteTimeUtc;
            private readonly string _content;
            private readonly XDocument _xml;
            private DateTime _lastAccessTimeUtc;

            public FileContentCache(string path, DateTime lastWriteTimeUtc, bool isXml = false)
            {
                var content = FileSystemHelpers.ReadAllText(path);
                var xml = isXml ? XDocument.Parse(content) : null;

                _path = path;
                _lastWriteTimeUtc = lastWriteTimeUtc;
                _lastAccessTimeUtc = DateTime.UtcNow;
                _content = content;
                _xml = xml;
            }

            public string Path => _path;
            public DateTime LastWriteTimeUtc => _lastWriteTimeUtc;
            public DateTime LastAccessTimeUtc => _lastAccessTimeUtc;

            public string Content 
            { 
                get
                {
                    _lastAccessTimeUtc = DateTime.UtcNow;
                    return _content;
                }
            }

            public XDocument Xml
            { 
                get
                {
                    Debug.Assert(_xml != null, "Must use FileSystemCache.ReadXml!");
                    _lastAccessTimeUtc = DateTime.UtcNow;
                    return _xml;
                }
            }
        }
    }
}
