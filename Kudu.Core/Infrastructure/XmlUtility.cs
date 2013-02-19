using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace Kudu.Core.Infrastructure
{
    internal static class XmlUtility
    {
        public static string Sanitize(string xml)
        {
            if (xml == null)
            {
                throw new ArgumentNullException("xml");
            }

            var buffer = new StringBuilder(xml.Length);

            var stringReader = new StringReader(xml);

            while (!stringReader.Done)
            {
                char c = stringReader.Read();

                if (IsLegalXmlChar(c))
                {
                    // Allow if the Unicode codepoint is legal in XML
                    buffer.Append(c);
                }
                else if (char.IsHighSurrogate(c) &&
                         !stringReader.Done &&
                         char.IsLowSurrogate(stringReader.Peek()))
                {
                    // Allow well-formed surrogate pairs
                    buffer.Append(c);
                    buffer.Append(stringReader.Read());
                }
            }

            return buffer.ToString();
        }

        private static bool IsLegalXmlChar(int character)
        {
            return
            (
                 character == 0x9 /* == '\t' == 9   */          ||
                 character == 0xA /* == '\n' == 10  */          ||
                 character == 0xD /* == '\r' == 13  */          ||
                (character >= 0x20 && character <= 0x7E)        ||
                 character == 0x85                              ||
                (character >= 0x100 && character <= 0xD7FF)     ||
                (character >= 0xE000 && character <= 0xFFFD)    ||
                 character >= 0x10000
            );
        }
    }
}
