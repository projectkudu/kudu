using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace Kudu.Core.Infrastructure.Test
{
    public class OperationManagerFacts
    {
        [Fact]
        public void AttemptExecutesAction()
        {
            // Arrange
            bool actionInvoked = false;
            Action action = () => { actionInvoked = true; };

            // Act
            OperationManager.Attempt(action);

            // Assert
            Assert.True(actionInvoked);
        }

        [Fact]
        public void AttemptRetriesAtMostSpecifiedTimesIfActionThrows()
        {
            // Arrange
            int numInvocations = 0;
            var exception = new Exception();
            Action action = () => { numInvocations++; throw exception; };

            // Act
            var output = Assert.Throws<Exception>(() => OperationManager.Attempt(action, retries: 2, delayBeforeRetry: 10));

            // Assert
            Assert.Same(exception, output);
            Assert.Equal(2, numInvocations);
        }

        [Fact]
        public void AttemptReturnsValuesIfMethodSucceeds()
        {
            // Arrange
            string expected = "Hello world";
            int numInvocations = 0;
            var exception = new Exception();
            Func<string> factory = () =>
            {
                numInvocations++;
                if (numInvocations <= 2)
                {
                    throw exception;
                }
                return expected;
            };

            // Act
            var output = OperationManager.Attempt(factory, retries: 3, delayBeforeRetry: 10);

            // Assert
            Assert.Equal(3, numInvocations);
            Assert.Equal(expected, output);
        }
    }
}