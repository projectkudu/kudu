using System;
using System.Collections.Specialized;
using System.Web;
using Moq;
using Xunit;

namespace Kudu.Services.Test
{
    public class InstanceIdUtilityFacts
    {
        [Fact]
        public void GetInstanceIdInternalThrowsIfContextIsNull()
        {
            // Act
            var ex = Assert.Throws<ArgumentNullException>(() => InstanceIdUtility.GetInstanceIdInternal(context: null, machineName: null));

            // Assert
            Assert.Equal("context", ex.ParamName);
        }

        [Fact]
        public void GetInstanceIdUsesLocalIdServerVariable()
        {
            // Arrange
            var serverVariables = new NameValueCollection();
            serverVariables["LOCAL_ADDR"] = "this-would-be-some-ip";
            var request = new Mock<HttpRequestBase>();
            request.Setup(r => r.ServerVariables).Returns(serverVariables);
            var context = new Mock<HttpContextBase>();
            context.Setup(c => c.Request).Returns(request.Object);

            var result = InstanceIdUtility.GetInstanceIdInternal(context.Object, "");

            // Assert
            Assert.Equal("f11600af8c2f753d24f85c01d217855fe65352b2d785057c2c1f2010e87dbea9", result);
        }

        [Fact]
        public void GetInstanceIdUsesMachineNameIfServerVariableIsUnavailable()
        {
            // Arrange
            var serverVariables = new NameValueCollection();
            var request = new Mock<HttpRequestBase>();
            request.Setup(r => r.ServerVariables).Returns(serverVariables);
            var context = new Mock<HttpContextBase>();
            context.Setup(c => c.Request).Returns(request.Object);

            var result = InstanceIdUtility.GetInstanceIdInternal(context.Object, "some-name");

            // Assert
            Assert.Equal("829611378102ce70a80e34fd0f614a95c68f8c0aa6a86456d61bc893edf787d0", result);
        }
    }
}
