using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Kudu.Contracts.Infrastructure;
using Kudu.Core.Infrastructure;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Kudu.Core.Settings
{
    /// <summary>
    /// Settings implementation backed by json persistent file and it is thread safe.
    /// We only supports flat key-value settings (no heirachy).
    /// </summary>
    public class JsonSettings
    {
        private readonly static TimeSpan _timeout = TimeSpan.FromSeconds(5);
        private readonly string _path;
        private LockFile _lock;

        public JsonSettings(string path)
        {
            _path = path;
            _lock = new LockFile(string.Format(CultureInfo.InvariantCulture, "{0}.lock", path));
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

        public void Save(JObject json)
        {
            _lock.LockOperation(() =>
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
            }, "Updating setting", _timeout);
        }

        public override string ToString()
        {
            // JObject.ToString() : Returns the indented JSON for this token.
            return Read().ToString(Formatting.None);
        }

        private JObject Read()
        {
            // need to check file exist before aquire lock
            // since aquire lock will generate lock file, and if folder not exist, will create folder
            if (!FileSystemHelpers.FileExists(_path))
            {
                return new JObject();
            }

            return _lock.LockOperation(() =>
            {
                // opens file for FileAccess.Read but does allow other read/write (FileShare.ReadWrite).
                // it is the most optimal where write is infrequent and dirty read is acceptable.
                using (var reader = new JsonTextReader(new StreamReader(FileSystemHelpers.OpenFile(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))))
                {
                    try
                    {
                        return JObject.Load(reader);
                    }
                    catch (JsonException)
                    {
                        // reset if corrupted.
                        return new JObject();
                    }
                }
            }, "Getting setting", _timeout);
        }
    }
}
