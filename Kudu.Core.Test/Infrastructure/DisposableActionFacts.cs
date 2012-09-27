using System;
using Xunit;

namespace Kudu.Core.Infrastructure.Test
{
    public class DisposableActionFacts
    {
        [Fact]
        public void DisposableActionInvokesActionOnDisposing()
        {
            // Arrange
            bool methodCalled = false;
            Action action = () => { methodCalled = true; };

            // Act
            using (new DisposableAction(action)) { }

            // Assert
            Assert.True(methodCalled);
        }
    }
}