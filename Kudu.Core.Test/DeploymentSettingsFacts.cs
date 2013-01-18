using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Web;
using Kudu.Contracts.Settings;
using Kudu.Core.SourceControl;
using Kudu.Core.SourceControl.Git;
using Kudu.Services.ServiceHookHandlers;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Extensions;

namespace Kudu.Core.Test
{
    public class DeploymentSettingFacts
    {
        [Theory, ClassData(typeof(CommandIdleTimeoutData))]
        public void CommandIdleTimeoutTests(string value, int expected)
        {
            // Act
            MockDeploymentSettingsManager settings = new MockDeploymentSettingsManager();
            settings.SetValue(SettingsKeys.CommandIdleTimeout, value);

            // Assert
            Assert.Equal(TimeSpan.FromSeconds(expected), settings.GetCommandIdleTimeout());
        }

        [Theory, ClassData(typeof(LogStreamTimeoutData))]
        public void LogStreamTimeoutTests(string value, int expected)
        {
            // Act
            MockDeploymentSettingsManager settings = new MockDeploymentSettingsManager();
            settings.SetValue(SettingsKeys.LogStreamTimeout, value);

            // Assert
            Assert.Equal(TimeSpan.FromSeconds(expected), settings.GetLogStreamTimeout());
        }

        [Theory, ClassData(typeof(TraceLevelData))]
        public void TraceLevelTests(string value, TraceLevel expected)
        {
            // Act
            MockDeploymentSettingsManager settings = new MockDeploymentSettingsManager();
            settings.SetValue(SettingsKeys.TraceLevel, value);

            // Assert
            Assert.Equal(expected, settings.GetTraceLevel());
        }

        class CommandIdleTimeoutData : SettingsData
        {
            protected override object DefaultValue
            {
                get { return (int)DeploymentSettingsExtension.DefaultCommandIdleTimeout.TotalSeconds; }
            }
        }

        class LogStreamTimeoutData : SettingsData
        {
            protected override object DefaultValue
            {
                get { return (int)DeploymentSettingsExtension.DefaultLogStreamTimeout.TotalSeconds; }
            }
        }

        class TraceLevelData : SettingsData
        {
            protected override object DefaultValue
            {
                get { return DeploymentSettingsExtension.DefaultTraceLevel; }
            }
        }

        abstract class SettingsData : IEnumerable<object[]>
        {
            protected abstract object DefaultValue { get; }

            public IEnumerator<object[]> GetEnumerator()
            { 
                yield return new object[] { "0", 0 };
                yield return new object[] { "-0", 0 };
                yield return new object[] { "4", 4 };
                yield return new object[] { "-4", 0 };
                yield return new object[] { "", DefaultValue };
                yield return new object[] { "a", DefaultValue };
                yield return new object[] { " ", DefaultValue };
                yield return new object[] { null, DefaultValue };
            }

            IEnumerator IEnumerable.GetEnumerator()
            { 
                return GetEnumerator(); 
            }
        }
    }
}
