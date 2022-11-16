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
            if (string.IsNullOrEmpty(section))
            {
                throw new ArgumentException("", "section");
            }
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("", "key");
            }
            try
            {
                return (from s in GetDocument()?.Root.Element(section)?.Elements("add")
                        where s.GetOptionalAttributeValue("key") == key
                        select s).FirstOrDefault()?.GetOptionalAttributeValue("value");
            }
            catch (Exception innerException)
            {
                throw new InvalidOperationException("Unable to parse settings file", innerException);
            }
        }

        public IList<KeyValuePair<string, string>> GetValues(string section)
        {
            if (string.IsNullOrEmpty(section))
            {
                throw new ArgumentException("", "section");
            }
            try
            {
                XDocument document = GetDocument();
                if (document == null)
                {
                    return null;
                }
                XElement xElement = document.Root.Element(section);
                if (xElement == null)
                {
                    return null;
                }
                List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>();
                foreach (XElement item in xElement.Elements("add"))
                {
                    string optionalAttributeValue = item.GetOptionalAttributeValue("key");
                    string optionalAttributeValue2 = item.GetOptionalAttributeValue("value");
                    if (!string.IsNullOrEmpty(optionalAttributeValue) && optionalAttributeValue2 != null)
                    {
                        list.Add(new KeyValuePair<string, string>(optionalAttributeValue, optionalAttributeValue2));
                    }
                }
                return list.AsReadOnly();
            }
            catch (Exception innerException)
            {
                throw new InvalidOperationException("Unable to parse settings file.", innerException);
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
            foreach (KeyValuePair<string, string> value in values)
            {
                SetValueInternal(section, value.Key, value.Value);
            }
        }

        private void SetValueInternal(string section, string key, string value)
        {
            if (string.IsNullOrEmpty(section))
            {
                throw new ArgumentException("", "section");
            }
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("", "key");
            }
            if (value == null)
            {
                throw new ArgumentNullException("value");
            }
            XDocument document = GetDocument(createIfNotExists: true);
            XElement xElement = document.Root.Element(section);
            if (xElement == null)
            {
                xElement = new XElement(section);
                document.Root.Add(xElement);
            }
            foreach (XElement item in xElement.Elements("add"))
            {
                string optionalAttributeValue = item.GetOptionalAttributeValue("key");
                if (optionalAttributeValue == key)
                {
                    item.SetAttributeValue("value", value);
                    Save(document);
                    return;
                }
            }
            XElement xElement2 = new XElement("add");
            xElement2.SetAttributeValue("key", key);
            xElement2.SetAttributeValue("value", value);
            xElement.Add(xElement2);
            Save(document);
        }

        public bool DeleteValue(string section, string key)
        {
            if (string.IsNullOrEmpty(section))
            {
                throw new ArgumentException("", "section");
            }
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("", "key");
            }
            XDocument document = GetDocument();
            if (document == null)
            {
                return false;
            }
            XElement xElement = document.Root.Element(section);
            if (xElement == null)
            {
                return false;
            }
            XElement xElement2 = null;
            foreach (XElement item in xElement.Elements("add"))
            {
                if (item.GetOptionalAttributeValue("key") == key)
                {
                    xElement2 = item;
                    break;
                }
            }
            if (xElement2 == null)
            {
                return false;
            }
            xElement2.Remove();
            Save(document);
            return true;
        }

        public bool DeleteSection(string section)
        {
            if (string.IsNullOrEmpty(section))
            {
                throw new ArgumentException("", "section");
            }
            XDocument document = GetDocument();
            if (document == null)
            {
                return false;
            }
            XElement xElement = document.Root.Element(section);
            if (xElement == null)
            {
                return false;
            }
            xElement.Remove();
            Save(document);
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
