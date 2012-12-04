using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using Kudu.Core.Infrastructure;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Kudu.Core.Settings
{
    /// <summary>
    /// Settings implementation backed by json persistent file.
    /// We only supports flat key-value settings (no heirachy).
    /// Concurrency is loosely provided at FileShare.ReadWrite.
    /// Trade-off with simplicity of synchronization across instances, we will allow 
    /// dirty read/write concurrently.
    /// </summary>
    public class JsonSettings
    {
        private string _path;
        private IFileSystem _fileSystem;

        public JsonSettings(string path)
            : this(new FileSystem(), path)
        {
        }

        public JsonSettings(IFileSystem fileSystem, string path)
        {
            _fileSystem = fileSystem;
            _path = path;
        }

        public string GetValue(string key)
        {
            return Read().Value<string>(key);
        }

        public IEnumerable<KeyValuePair<string, string>> GetValues()
        {
            var dict = (IDictionary<string, JToken>)Read();
            return dict.ToDictionary(pair => pair.Key, pair => pair.Value.Value<string>());
        }

        public void SetValue(string key, string value)
        {
            JObject json = Read();
            json[key] = value;
            Save(json);
        }

        public void SetValues(JObject values)
        {
            JObject json = Read();
            foreach (KeyValuePair<string, JToken> pair in values)
            {
                json[pair.Key] = pair.Value.Value<string>();
            }

            Save(json);
        }

        public void SetValues(IEnumerable<KeyValuePair<string, string>> values)
        {
            JObject json = Read();
            foreach (KeyValuePair<string, string> pair in values)
            {
                json[pair.Key] = pair.Value;
            }

            Save(json);
        }

        public bool DeleteValue(string key)
        {
            JObject json = Read();
            if (json.Remove(key))
            {
                Save(json);
                return true;
            }

            return false;
        }

        private JObject Read()
        {
            if (!_fileSystem.File.Exists(_path))
            {
                return new JObject();
            }

            using (var reader = new JsonTextReader(new StreamReader(_fileSystem.File.Open(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))))
            {
                return JObject.Load(reader);
            }
        }

        private void Save(JObject json)
        {
            if (!_fileSystem.File.Exists(_path))
            {
                FileSystemHelpers.EnsureDirectory(_fileSystem, Path.GetDirectoryName(_path));
            }

            using (var writer = new StreamWriter(_fileSystem.File.Open(_path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)))
            {
                // prefer indented-readable format
                writer.Write(json.ToString());
            }
        }
    }
}
