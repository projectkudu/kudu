using System.Xml.Linq;

namespace Kudu.Core.Xml
{
    internal static class XElementExtensions
    {
        public static string GetOptionalAttributeValue(this XElement element, string localName, string namespaceName = null)
        {
            return ((!string.IsNullOrEmpty(namespaceName)) ? element.Attribute(XName.Get(localName, namespaceName)) : element.Attribute(localName))?.Value;
        }

        public static string GetOptionalElementValue(this XElement element, string localName, string namespaceName = null)
        {
            return ((!string.IsNullOrEmpty(namespaceName)) ? element.Element(XName.Get(localName, namespaceName)) : element.Element(localName))?.Value;
        }
    }
}
