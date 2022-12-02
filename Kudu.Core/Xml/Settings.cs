using NuGet.Packaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Kudu.Core.Xml
{
    public class Settings : ISettings
    {
        private readonly string _path;

        public Settings(string path)
        {
            _path = path;
        }

        public string GetValue(string section, string key)
        {
            if (String.IsNullOrEmpty(section))
            {
                throw new ArgumentException("", "section");
            }

            if (String.IsNullOrEmpty(key))
            {
                throw new ArgumentException("", "key");
            }

            try
            {
                var document = GetDocument();

                if (document == null)
                {
                    return null;
                }

                // Get the section and return null if it doesnt exist
                var sectionElement = document.Root.Element(section);
                if (sectionElement == null)
                {
                    return null;
                }

                // Get the add element that matches the key and return null if it doesnt exist
                var element = sectionElement.Elements("add").Where(s => s.GetOptionalAttributeValue("key") == key).FirstOrDefault();
                if (element == null)
                {
                    return null;
                }

                // Return the optional value which if not there will be null;
                return element.GetOptionalAttributeValue("value");
            }
            catch (Exception e)
            {
                throw new InvalidOperationException("Unable to parse settings file", e);
            }
        }

        public IList<KeyValuePair<string, string>> GetValues(string section)
        {
            if (String.IsNullOrEmpty(section))
            {
                throw new ArgumentException("", "section");
            }

            try
            {
                XDocument config = GetDocument();

                if (config == null)
                {
                    return null;
                }

                XElement sectionElement = config.Root.Element(section);
                if (sectionElement == null)
                {
                    return null;
                }

                var kvps = new List<KeyValuePair<string, string>>();
                foreach (var e in sectionElement.Elements("add"))
                {
                    var key = e.GetOptionalAttributeValue("key");
                    var value = e.GetOptionalAttributeValue("value");
                    if (!String.IsNullOrEmpty(key) && value != null)
                    {
                        kvps.Add(new KeyValuePair<string, string>(key, value));
                    }
                }

                return kvps.AsReadOnly();
            }
            catch (Exception e)
            {
                throw new InvalidOperationException("Unable to parse settings file.", e);
            }
        }

        public void SetValue(string section, string key, string value)
        {
            SetValueInternal(section, key, value);
        }

        public void SetValues(string section, IList<KeyValuePair<string, string>> values)
        {
            if (values == null)
            {
                throw new ArgumentNullException("values");
            }

            foreach (var kvp in values)
            {
                SetValueInternal(section, kvp.Key, kvp.Value);
            }
        }

        private void SetValueInternal(string section, string key, string value)
        {
            if (String.IsNullOrEmpty(section))
            {
                throw new ArgumentException("", "section");
            }

            if (String.IsNullOrEmpty(key))
            {
                throw new ArgumentException("", "key");
            }

            if (value == null)
            {
                throw new ArgumentNullException("value");
            }

            XDocument config = GetDocument(createIfNotExists: true);

            XElement sectionElement = config.Root.Element(section);
            if (sectionElement == null)
            {
                sectionElement = new XElement(section);
                config.Root.Add(sectionElement);
            }

            foreach (var e in sectionElement.Elements("add"))
            {
                var tempKey = e.GetOptionalAttributeValue("key");

                if (tempKey == key)
                {
                    e.SetAttributeValue("value", value);
                    Save(config);
                    return;
                }
            }

            var addElement = new XElement("add");
            addElement.SetAttributeValue("key", key);
            addElement.SetAttributeValue("value", value);
            sectionElement.Add(addElement);
            Save(config);
        }

        public bool DeleteValue(string section, string key)
        {
            if (String.IsNullOrEmpty(section))
            {
                throw new ArgumentException("", "section");
            }

            if (String.IsNullOrEmpty(key))
            {
                throw new ArgumentException("", "key");
            }

            XDocument config = GetDocument();

            if (config == null)
            {
                return false;
            }

            XElement sectionElement = config.Root.Element(section);
            if (sectionElement == null)
            {
                return false;
            }

            XElement elementToDelete = null;
            foreach (var e in sectionElement.Elements("add"))
            {
                if (e.GetOptionalAttributeValue("key") == key)
                {
                    elementToDelete = e;
                    break;
                }
            }

            if (elementToDelete == null)
            {
                return false;
            }

            elementToDelete.Remove();
            Save(config);
            return true;

        }

        public bool DeleteSection(string section)
        {
            if (String.IsNullOrEmpty(section))
            {
                throw new ArgumentException("", "section");
            }

            XDocument config = GetDocument();

            if (config == null)
            {
                return false;
            }

            XElement sectionElement = config.Root.Element(section);
            if (sectionElement == null)
            {
                return false;
            }

            sectionElement.Remove();
            Save(config);
            return true;
        }

        private void Save(XDocument document)
        {
            document.Save(_path);
        }

        private XDocument GetDocument(bool createIfNotExists = false)
        {
            return XmlUtility.GetDocument("settings", _path, createIfNotExists);
        }
    }
}
