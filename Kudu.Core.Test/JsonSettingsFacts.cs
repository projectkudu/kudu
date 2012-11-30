using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
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
            IFileSystem fileSystem = GetMockFileSystem(SettingsPath);

            var settings = new JsonSettings(fileSystem, SettingsPath);

            Assert.Equal(null, settings.GetValue("non_existing"));

            Assert.Equal(0, settings.GetValues().Count());

            Assert.False(settings.DeleteValue("non_existing"));

            Assert.False(fileSystem.File.Exists(SettingsPath));
        }

        [Fact]
        public void ConstructorWithValuesTest()
        {
            var values = new[]
            {
                new KeyValuePair<string, string>(Guid.NewGuid().ToString(), Guid.NewGuid().ToString()),
                new KeyValuePair<string, string>(Guid.NewGuid().ToString(), Guid.NewGuid().ToString())
            };

            var settings = new JsonSettings(GetMockFileSystem(SettingsPath, values), SettingsPath);

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
            var value = new KeyValuePair<string, string>(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());
            var settings = new JsonSettings(GetMockFileSystem(SettingsPath), SettingsPath);

            Assert.Equal(null, settings.GetValue(value.Key));

            settings.SetValue(value.Key, value.Value);

            Assert.Equal(value.Value, settings.GetValue(value.Key));
        }

        [Fact]
        public void SetGetValuesTest()
        {
            var values = new Dictionary<string, string>
            {
                { Guid.NewGuid().ToString(), Guid.NewGuid().ToString() },
                { Guid.NewGuid().ToString(), Guid.NewGuid().ToString() },
                { Guid.NewGuid().ToString(), Guid.NewGuid().ToString() }
            };

            var settings = new JsonSettings(GetMockFileSystem(SettingsPath), SettingsPath);

            Assert.Equal(0, settings.GetValues().Count());

            settings.SetValues(values);

            Assert.Equal(values.Count, settings.GetValues().Count());

            foreach (KeyValuePair<string, string> value in settings.GetValues())
            {
                Assert.Equal(values[value.Key], value.Value);
            }

            // Update
            values[values.Keys.ElementAt(0)] = Guid.NewGuid().ToString();

            settings.SetValues(values);

            foreach (KeyValuePair<string, string> value in settings.GetValues())
            {
                Assert.Equal(values[value.Key], value.Value);
            }
        }

        [Fact]
        public void SetGetJObjectTest()
        {
            var values = new Dictionary<string, string>
            {
                { Guid.NewGuid().ToString(), null },
                { Guid.NewGuid().ToString(), String.Empty },
                { Guid.NewGuid().ToString(), Guid.NewGuid().ToString() }
            };

            JObject json = new JObject();
            foreach (KeyValuePair<string, string> value in values)
            {
                json[value.Key] = value.Value;
            }

            var settings = new JsonSettings(GetMockFileSystem(SettingsPath), SettingsPath);

            Assert.Equal(0, settings.GetValues().Count());

            settings.SetValues(json);

            Assert.Equal(values.Count, settings.GetValues().Count());

            foreach (KeyValuePair<string, string> value in settings.GetValues())
            {
                Assert.Equal(json[value.Key].Value<string>(), value.Value);
            }
        }

        [Fact]
        public void NullOrEmptyTest()
        {
            var key = Guid.NewGuid().ToString();
            var settings = new JsonSettings(GetMockFileSystem(SettingsPath), SettingsPath);

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
            var settings = new JsonSettings(GetMockFileSystem(SettingsPath), SettingsPath);

            Assert.Equal(null, settings.GetValue(value.Key));

            settings.SetValue(value.Key, value.Value);
            Assert.Equal(value.Value, settings.GetValue(value.Key));

            // Delete existing value
            Assert.Equal(true, settings.DeleteValue(value.Key));

            Assert.Equal(null, settings.GetValue(value.Key));

            // Delete non-existing value
            Assert.False(settings.DeleteValue(value.Key));
        }

        private IFileSystem GetMockFileSystem(string path, params KeyValuePair<string, string>[] values)
        {
            MemoryStream mem = null;
            var fileSystem = new Mock<IFileSystem>();
            var file = new Mock<FileBase>();
            var directory = new Mock<DirectoryBase>();

            // Arrange
            file.Setup(f => f.Open(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)).Returns(() => 
            {
                mem = new MemoryStream();
                return mem;
            });
            file.Setup(f => f.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)).Returns(() =>
            {
                if (mem == null)
                {
                    throw new FileNotFoundException(path + " does not exist.");
                }

                byte[] bytes = mem.GetBuffer();
                mem = new MemoryStream(bytes, 0, bytes.Length, writable: false, publiclyVisible: true);
                return mem;
            });

            if (values.Length > 0)
            {
                JObject json = new JObject();
                foreach (KeyValuePair<string, string> pair in values)
                {
                    json[pair.Key] = pair.Value;
                }

                mem = new MemoryStream();
                using (var writer = new JsonTextWriter(new StreamWriter(mem)))
                {
                    json.WriteTo(writer);
                }
            }

            file.Setup(f => f.Exists(path)).Returns(() => mem != null);

            fileSystem.Setup(fs => fs.File).Returns(file.Object);
            fileSystem.Setup(fs => fs.Directory).Returns(directory.Object);

            return fileSystem.Object;
        }
    }
}
