using System;
using System.Collections.Generic;
using System.Linq;
using Kudu.Core.Infrastructure;
using Xunit;

namespace Kudu.Core.Test.Infrastructure.cs
{
    public class XmlUtilityFacts
    {
        [Theory]
        [MemberData("ValidCharacterSet")]
        public void XmlUtilityReturnsValidCharacterStringsUnchanged(string input)
        {
            // Act
            string output = XmlUtility.Sanitize(input);

            // Assert
            Assert.Equal(input, output);
        }

        // cannot use [Theory, InlineData] due to https://github.com/xunit/xunit/issues/380
        [Fact]
        public void XmlUtilityRemovesInvalidCharacters()
        {
            foreach (var inputs in InvalidCharacterSet)
            {
                // Act
                string output = XmlUtility.Sanitize((string)inputs[0]);

                // Assert
                Assert.Equal("ABC", output);
            }
        }

        [Fact]
        public void TestXmlUtilityGlob()
        {
            Assert.Equal(XmlUtility.Sanitize("𠀁𠀂𠀃𪛑𪛒𪛓"), "𠀁𠀂𠀃𪛑𪛒𪛓");
        }

        [Fact]
        public void TestXmlUtilityEmptyString()
        {
            Assert.Equal(XmlUtility.Sanitize("\u0000\u0001\u000b\u000e\u007f\u0086\uFFFE"), String.Empty);
        }

        [Fact]
        public void TestXmlUtilityDC00()
        {
            Assert.Equal(XmlUtility.Sanitize("\xD834AB\xDC00C\xDD1E"), "ABC");
        }

        public static IEnumerable<object[]> ValidCharacterSet
        {
            get
            {
                yield return new object[] { "Hello world" };
                yield return new object[] { "Hello\nworld" };
                yield return new object[] { "Hello\r\nworld" };
                yield return new object[] { "Hello\tworld" };
                yield return new object[] { "foo\xD834\xDD1Ebar" };
                yield return new object[] { GetString(0xD700, 0xFF) };
                yield return new object[] { GetString(0xE000, 0xFF) };
                yield return new object[] { GetString(char.MaxValue - 0xFF, 0xFE) };
            }
        }

        public static IEnumerable<object[]> InvalidCharacterSet
        {
            get
            {
                yield return new object[] { new string(new[] { 'A', 'B', (char)11, 'C', (char)19 }) };
                yield return new object[] { "\u0000\u0001\u000bAB\u000e\u007f\u0086C\uFFFE" };
                yield return new object[] { new string(new[] { 'A', '\x000B', 'B', 'C' }) };
            }
        }

        private static string GetString(int start, int length)
        {
            return new String(Enumerable.Range(start, length).Select(Convert.ToChar).ToArray());
        }
    }
}
