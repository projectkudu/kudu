using System;
using System.Text.RegularExpressions;

namespace Kudu.Core
{
    public static class XmlUtils
    {
        // http://stackoverflow.com/questions/397250/unicode-regex-invalid-xml-characters
        // filters control characters but allows only properly-formed surrogate sequences
        // #x9 | #xA | #xD | [#x20-#xD7FF] | [#xE000-#xFFFD] | [#x10000-#x10FFFF]
        private static Regex _invalidXMLChars = new Regex(
            @"(?<![\uD800-\uDBFF])[\uDC00-\uDFFF]|[\uD800-\uDBFF](?![\uDC00-\uDFFF])|[\x00-\x08\x0B\x0C\x0E-\x1F\x7F-\x9F\uFEFF\uFFFE\uFFFF]",
            RegexOptions.Compiled);

        public static string RemoveInvalidXMLChars(string text)
        {
            if (String.IsNullOrEmpty(text))
            {
                return text;
            }

            return _invalidXMLChars.Replace(text, "?");
        }
    }
}