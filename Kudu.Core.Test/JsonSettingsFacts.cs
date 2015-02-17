using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using Kudu.Core.Infrastructure;
using Kudu.Core.Settings;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Kudu.Core.Test
{
    public class JsonSettingsFacts
    {
        const string SettingsPath = @"a:\test\settings.json";

        [Fact]
        public void ConstructorTest()
        {
            FileSystemHelpers.Instance = GetMockFileSystem(SettingsPath);

            var settings = new JsonSettings(SettingsPath);

            Assert.Equal(null, settings.GetValue("non_existing"));

            Assert.Equal(0, settings.GetValues().Count());

            Assert.False(settings.DeleteValue("non_existing"));

            Assert.False(FileSystemHelpers.FileExists(SettingsPath));
        }

        [Fact]
        public void ConstructorWithValuesTest()
        {
            var values = new[]
            {
                new KeyValuePair<string, string>(Guid.NewGuid().ToString(), Guid.NewGuid().ToString()),
                new KeyValuePair<string, string>(Guid.NewGuid().ToString(), Guid.NewGuid().ToString())
            };

            FileSystemHelpers.Instance = GetMockFileSystem(SettingsPath, values);

            var settings = new JsonSettings(SettingsPath);

            foreach (KeyValuePair<string, string> value in values)
            {
                Assert.Equal(value.Value, settings.GetValue(value.Key));
            }

            Assert.Equal(null, settings.GetValue("non_existing"));

            Assert.Equal(values.Length, settings.GetValues().Count());
        }

        [Fact]
        public void SetGetValueTest()
        {
            Random random = new Random((int)DateTime.Now.Ticks);
            var values = new Dictionary<string, JToken>
            {
                { Guid.NewGuid().ToString(), Guid.NewGuid().ToString() },
                { Guid.NewGuid().ToString(), random.Next() },
                { Guid.NewGuid().ToString(), random.Next() % 2 == 0 }
            };

            FileSystemHelpers.Instance = GetMockFileSystem(SettingsPath);

            var settings = new JsonSettings(SettingsPath);

            foreach (KeyValuePair<string, JToken> value in values)
            {
                Assert.Equal(null, settings.GetValue(value.Key));

                settings.SetValue(value.Key, value.Value);

                Assert.Equal(value.Value, settings.GetValue(value.Key));
            }
        }

        [Fact]
        public void SetGetValuesTest()
        {
            Random random = new Random((int)DateTime.Now.Ticks);
            var values = new Dictionary<string, JToken>
            {
                { Guid.NewGuid().ToString(), Guid.NewGuid().ToString() },
                { Guid.NewGuid().ToString(), random.Next() },
                { Guid.NewGuid().ToString(), random.Next() % 2 == 0 }
            };

            FileSystemHelpers.Instance = GetMockFileSystem(SettingsPath);

            var settings = new JsonSettings(SettingsPath);

            Assert.Equal(0, settings.GetValues().Count());

            settings.SetValues(values);

            Assert.Equal(values.Count, settings.GetValues().Count());

            foreach (KeyValuePair<string, JToken> value in settings.GetValues())
            {
                Assert.Equal(values[value.Key], value.Value);
            }

            // Update
            values[values.Keys.ElementAt(0)] = Guid.NewGuid().ToString();

            settings.SetValues(values);

            foreach (KeyValuePair<string, JToken> value in settings.GetValues())
            {
                Assert.Equal(values[value.Key], value.Value);
            }
        }

        [Fact]
        public void SetGetJObjectTest()
        {
            var values = new Dictionary<string, JToken>
            {
                { Guid.NewGuid().ToString(), null },
                { Guid.NewGuid().ToString(), String.Empty },
                { Guid.NewGuid().ToString(), Guid.NewGuid().ToString() }
            };

            JObject json = new JObject();
            foreach (KeyValuePair<string, JToken> value in values)
            {
                json[value.Key] = value.Value;
            }

            FileSystemHelpers.Instance = GetMockFileSystem(SettingsPath);

            var settings = new JsonSettings(SettingsPath);

            Assert.Equal(0, settings.GetValues().Count());

            settings.SetValues(json);

            Assert.Equal(values.Count, settings.GetValues().Count());

            foreach (KeyValuePair<string, JToken> value in settings.GetValues())
            {
                Assert.Equal(json[value.Key], value.Value);
            }
        }

        [Fact]
        public void NullOrEmptyTest()
        {
            var key = Guid.NewGuid().ToString();
            FileSystemHelpers.Instance = GetMockFileSystem(SettingsPath);
            var settings = new JsonSettings(SettingsPath);

            Assert.Equal(null, settings.GetValue(key));

            settings.SetValue(key, String.Empty);
            Assert.Equal(String.Empty, settings.GetValue(key));

            settings.SetValue(key, null);
            Assert.Equal(null, settings.GetValue(key));
        }

        [Fact]
        public void DeleteValueTest()
        {
            var value = new KeyValuePair<string, string>(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());
            FileSystemHelpers.Instance = GetMockFileSystem(SettingsPath);

            var settings = new JsonSettings(SettingsPath);

            Assert.Equal(null, settings.GetValue(value.Key));

            settings.SetValue(value.Key, value.Value);
            Assert.Equal(value.Value, settings.GetValue(value.Key));

            // Delete existing value
            Assert.Equal(true, settings.DeleteValue(value.Key));

            Assert.Equal(null, settings.GetValue(value.Key));

            // Delete non-existing value
            Assert.False(settings.DeleteValue(value.Key));
        }

        private IFileSystem GetMockFileSystem(string filePath, params KeyValuePair<string, string>[] values)
        {
            var files = new Dictionary<string, MemoryStream>();
            var fs = new Mock<IFileSystem>(MockBehavior.Strict);
            var fileBase = new Mock<FileBase>(MockBehavior.Strict);
            var dirBase = new Mock<DirectoryBase>(MockBehavior.Strict);

            // Setup
            fs.SetupGet(f => f.File)
              .Returns(() => fileBase.Object);
            fs.SetupGet(f => f.Directory)
              .Returns(() => dirBase.Object);

            fileBase.Setup(f => f.Exists(It.IsAny<string>()))
                    .Returns((string path) => files.ContainsKey(path));
            fileBase.Setup(f => f.Open(It.IsAny<string>(), FileMode.Create, FileAccess.Write, FileShare.Read))
                    .Returns((string path, FileMode fileMode, FileAccess fileAccess, FileShare fileShare) =>
                    {
                        files[path] = MockMemoryStream();
                        return files[path];
                    });

            fileBase.Setup(f => f.Open(It.IsAny<string>(), FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                .Returns((string path, FileMode fileMode, FileAccess fileAccess, FileShare fileShare) =>
                {
                    MemoryStream stream = files[path];
                    stream.Position = 0;
                    return stream;
                });

            dirBase.Setup(d => d.Exists(It.IsAny<string>()))
                .Returns((string dirPath) =>
                {
                    return files.Keys.FirstOrDefault(f => f.StartsWith(dirPath)) != null;
                });

            dirBase.Setup(d => d.CreateDirectory(It.IsAny<string>()))
                .Returns((string path) =>
                {
                    return Mock.Of<DirectoryInfoBase>();
                });

            // populate default data if any
            if (values.Length > 0)
            {
                JObject json = new JObject();
                foreach (KeyValuePair<string, string> pair in values)
                {
                    json[pair.Key] = pair.Value;
                }

                MemoryStream mem;
                if (!files.TryGetValue(filePath, out mem))
                {
                    mem = MockMemoryStream();
                    files[filePath] = mem;
                }
                using (var writer = new JsonTextWriter(new StreamWriter(mem)))
                {
                    json.WriteTo(writer);
                }
            }

            return fs.Object;
        }

        private MemoryStream MockMemoryStream()
        {
            // Override Close with no-op
            var stream = new Mock<MemoryStream> { CallBase = true };
            stream.Setup(s => s.Close());
            return stream.Object;
        }
    }
}
