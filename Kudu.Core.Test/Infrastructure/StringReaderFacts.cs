using System;
using Xunit;

namespace Kudu.Core.Infrastructure.Test
{
    public class StringReaderFacts
    {
        [Fact]
        public void ConstructorThrowsIfStringIsNull()
        {
            // Act and Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new StringReader(raw: null));
            Assert.Equal("raw", ex.ParamName);
        }

        [Theory]
        [InlineData("Hello\r\nWorld", "Hello\r\n")]
        [InlineData("Hello\nWorld", "Hello\n")]
        [InlineData("Hello\rWorld", "Hello\rWorld")]
        [InlineData("", null)]
        [InlineData("Hello\nWorld\r\n", "Hello\n")]
        public void ReadLineReadsUntilFirstNewLineCharacter(string input, string expected)
        {
            // Arrange
            var stringReader = new StringReader(input);

            // Act
            string actual = stringReader.ReadLine();

            // Assert
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("Helloworld", "Hello", "world")]
        [InlineData("Hello", "Hello", null)]
        [InlineData("Hell", null, null)]
        public void ReadReadsExactlyNCharacters(string input, string expected1, string expected2)
        {
            // Arrange
            var stringReader = new StringReader(input);

            // Act
            string actual1 = stringReader.Read(n: 5);
            string actual2 = stringReader.Read(n: 5);

            // Assert
            Assert.Equal(expected1, actual1);
            Assert.Equal(expected2, actual2);
        }

        [Theory]
        [InlineData("Hello", 'H', 'e')]
        [InlineData("H", 'H', '\0')]
        [InlineData("", '\0', '\0')]
        public void ReadReadsCharacter(string input, char expected1, char expected2)
        {
            // Arrange
            var stringReader = new StringReader(input);

            // Act
            char actual1 = stringReader.Read();
            char actual2 = stringReader.Read();

            // Assert
            Assert.Equal(expected1, actual1);
            Assert.Equal(expected2, actual2);
        }

        [Theory]
        [InlineData("", 0)]
        [InlineData("Abc", 0)]
        [InlineData("123", 123)]
        [InlineData("123 Abc", 123)]
        [InlineData("123Abc", 123)]
        public void ReadIntReadsFullIntegerValues(string input, int expected)
        {
            // Arrange
            var stringReader = new StringReader(input);

            // Act
            int actual = stringReader.ReadInt();

            // Assert
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("", null)]
        [InlineData(" abc", "")]
        [InlineData("Ab cdef ", "Ab")]
        [InlineData("Ab  ", "Ab")]
        public void ReadUntilWhiteSpaceWorks(string input, string expected)
        {
            // Arrange
            var stringReader = new StringReader(input);

            // Act
            string actual = stringReader.ReadUntilWhitespace();

            // Assert
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("Hello", 'e', "H")]
        [InlineData("Hello", 'l', "He")]
        [InlineData("Hello", 'z', "Hello")]
        public void ReadUntilReadsUntilItEncountersFirstInstanceOfSpecifiedCharacter(string input, char ch, string expected)
        {
            // Arrange
            var stringReader = new StringReader(input);

            // Act
            string actual = stringReader.ReadUntil(ch);

            // Assert
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("Hello", "llo", "He")]
        [InlineData("Hello", "el", "H")]
        [InlineData("Hello", "Hello", "")]
        [InlineData("Hello", "abcd", "Hello")]
        [InlineData("ab", "abcd", "ab")]
        public void ReadUntilReadsUntilItEncountersFirstInstanceOfString(string input, string searchString, string expected)
        {
            // Arrange
            var stringReader = new StringReader(input);

            // Act
            string actual = stringReader.ReadUntil(searchString);

            // Assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ReadToEndReadsTheEntireString()
        {
            // Arrange
            var stringReader = new StringReader("123 Hello\r\n 123 world");

            // Act
            stringReader.ReadInt();
            string output = stringReader.ReadToEnd();

            // Assert
            Assert.Equal(" Hello\r\n 123 world", output);
        }

        [Theory]
        [InlineData("abcd", 'b', false)]
        [InlineData("cd", 'd', true)]
        public void SkipWorks(string input, char expected, bool done)
        {
            // Arrange
            var stringReader = new StringReader(input);

            // Act
            stringReader.Skip();

            // Assert
            Assert.Equal(expected, stringReader.Read());
            Assert.Equal(done, stringReader.Done);
        }

        [Theory]
        [InlineData("abcd", 3, 'd')]
        [InlineData("cd", 4, '\0')]
        public void SkipNWorks(string input, int n, char expected)
        {
            // Arrange
            var stringReader = new StringReader(input);

            // Act
            stringReader.Skip(n);

            // Assert
            Assert.Equal(expected, stringReader.Read());
        }

        [Theory]
        [InlineData("abcd", "ab", "cd")]
        [InlineData("abcd", "abd", "abcd")]
        [InlineData("abcd", "abcd", null)]
        public void SkipStringWorks(string input, string skip, string expected)
        {
            // Arrange
            var stringReader = new StringReader(input);

            // Act
            stringReader.Skip(skip);

            // Assert
            Assert.Equal(expected, stringReader.ReadToEnd());
        }

        [Fact]
        public void ToStringReturnsReaminingString()
        {
            // Arrange
            var stringReader = new StringReader("abcd");

            // Act
            stringReader.Skip();
            var actual = stringReader.ToString();

            // Assert
            Assert.Equal("bcd", actual);
        }
    }
}
