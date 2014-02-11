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

        public JsonSettings(string path)
        {
            _path = path;
        }

        public string GetValue(string key)
        {
            return Read().Value<string>(key);
        }

        public IEnumerable<KeyValuePair<string, JToken>> GetValues()
        {
            var dict = (IDictionary<string, JToken>)Read();
            return dict.ToDictionary(pair => pair.Key, pair => pair.Value);
        }

        public void SetValue(string key, JToken value)
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
                json[pair.Key] = pair.Value;
            }

            Save(json);
        }

        public void SetValues(IEnumerable<KeyValuePair<string, JToken>> values)
        {
            JObject json = Read();
            foreach (KeyValuePair<string, JToken> pair in values)
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
            if (!FileSystemHelpers.FileExists(_path))
            {
                return new JObject();
            }

            // opens file for FileAccess.Read but does allow other read/write (FileShare.ReadWrite).   
            // it is the most optimal where write is infrequent and dirty read is acceptable.
            using (var reader = new JsonTextReader(new StreamReader(FileSystemHelpers.OpenFile(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))))
            {
                return JObject.Load(reader);
            }
        }

        private void Save(JObject json)
        {
            if (!FileSystemHelpers.FileExists(_path))
            {
                FileSystemHelpers.EnsureDirectory(Path.GetDirectoryName(_path));
            }

            // opens file for FileAccess.Write but does allow other dirty read (FileShare.Read).   
            // it is the most optimal where write is infrequent and dirty read is acceptable.
            using (var writer = new JsonTextWriter(new StreamWriter(FileSystemHelpers.OpenFile(_path, FileMode.Create, FileAccess.Write, FileShare.Read))))
            {
                // prefer indented-readable format
                writer.Formatting = Formatting.Indented;
                json.WriteTo(writer);
            }
        }
    }
}
