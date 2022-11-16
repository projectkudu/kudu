using System.IO;
using System.Xml.Linq;

namespace Kudu.Core.Xml
{
    internal static class XmlUtility
    {
        internal static XDocument GetDocument(XName rootName, string path, bool createIfNotExists)
        {
            if (File.Exists(path))
            {
                try
                {
                    return GetDocument(path);
                }
                catch (FileNotFoundException)
                {
                    if (createIfNotExists)
                    {
                        return CreateDocument(rootName, path);
                    }
                }
            }
            if (createIfNotExists)
            {
                return CreateDocument(rootName, path);
            }
            return null;
        }

        private static XDocument CreateDocument(XName rootName, string path)
        {
            XDocument xDocument = new XDocument(new XElement(rootName));
            xDocument.Save(path);
            return xDocument;
        }

        internal static XDocument GetDocument(string path)
        {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                return XDocument.Load((Stream)stream);
            }           
        }
    }
}
