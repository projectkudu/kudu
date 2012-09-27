using System;
using System.Collections.Generic;
using System.Linq;
using Kudu.Core.Infrastructure;
using Xunit;
using Xunit.Extensions;

namespace Kudu.Core.Test.Infrastructure.cs
{
    public class XmlUtilityFacts
    {
        [Theory]
        [PropertyData("ValidCharacterSet")]
        public void XmlUtilityReturnsValidCharacterStringsUnchanged(string input)
        {
            // Act
            string output = XmlUtility.Sanitize(input);

            // Assert
            Assert.Equal(input, output);
        }

        [Theory]
        [PropertyData("InvalidCharacterSet")]
        public void XmlUtilityRemovesInvalidCharacters(string input)
        {
            // Act
            string output = XmlUtility.Sanitize(input);

            // Assert
            Assert.Equal("ABC", output);
        }

        public static IEnumerable<object[]> ValidCharacterSet
        {
            get
            {
                yield return new object[] { "Hello world" };
                yield return new object[] { "Hello\r\nworld" };
                yield return new object[] { "Hello\tworld" };
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
                yield return new object[] { new string(new[] { 'A', 'B', (char)(0xD7FF + 1), (char)(0xDC00), 'C' }) };
            }
        }

        private static string GetString(int start, int length)
        {
            return new String(Enumerable.Range(start, length).Select(Convert.ToChar).ToArray());
        }
    }
}
