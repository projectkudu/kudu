using System;
using System.Collections.Generic;
using Kudu.Contracts.Settings;
using Kudu.Core.Settings;
using Xunit;

namespace Kudu.Core.Test.Settings
{
    public class DeploymentSettingsPrioritiesTests
    {
        private DeploymentSettingsManager deploymentSettingsManager;

        public DeploymentSettingsPrioritiesTests()
        {
            var settingsPriority1 = new Dictionary<string, string>();
            settingsPriority1["key1"] = "settingsPriority1_value1";
            settingsPriority1["key2"] = "settingsPriority1_value2";
            settingsPriority1["key3"] = "settingsPriority1_value3";
            settingsPriority1["key4"] = "settingsPriority1_value4";

            var settingsPriority2 = new Dictionary<string, string>();
            settingsPriority1["key2"] = "settingsPriority2_value2";
            settingsPriority1["key3"] = "settingsPriority2_value3";
            settingsPriority1["key5"] = "settingsPriority2_value5";
            settingsPriority1["key6"] = "settingsPriority2_value6";

            var settingsPriority50 = new Dictionary<string, string>();
            settingsPriority1["key1"] = "settingsPriority50_value1";
            settingsPriority1["key2"] = "settingsPriority50_value2";
            settingsPriority1["key5"] = "settingsPriority50_value5";
            settingsPriority1["key7"] = "settingsPriority50_value7";

            var testProvider1 = new BasicSettingsProvider(settingsPriority1, (SettingsProvidersPriority)1);
            var testProvider2 = new BasicSettingsProvider(settingsPriority2, (SettingsProvidersPriority)2);
            var testProvider50 = new BasicSettingsProvider(settingsPriority50, (SettingsProvidersPriority)50);
            var settingsProviders = new ISettingsProvider[] { testProvider1, testProvider50, testProvider2 };

            PerSiteSettingsProvider perSiteSettings = null;
            deploymentSettingsManager = new DeploymentSettingsManager(perSiteSettings, settingsProviders);
        }

        [Fact]
        public void DeploymentSettingsTopPriorityOverridesAllOthers()
        {
            AssertSetting(50, 1);
        }

        [Fact]
        public void DeploymentSettingsTopPriorityOverridesLastPriority()
        {
            AssertSetting(50, 2);
        }

        [Fact]
        public void DeploymentSettingsMiddlePriorityOverridesLastPriority()
        {
            AssertSetting(2, 3);
        }

        [Fact]
        public void DeploymentSettingsLastPriorityUsedWhenNoOthers()
        {
            AssertSetting(1, 4);
        }

        [Fact]
        public void DeploymentSettingsTopPriorityOverridesMiddlePriority()
        {
            AssertSetting(50, 5);
        }

        [Fact]
        public void DeploymentSettingsMiddlePriorityUsedWhenNoOthers()
        {
            AssertSetting(2, 6);
        }

        [Fact]
        public void DeploymentSettingsTopPriorityUsedWhenNoOthers()
        {
            AssertSetting(50, 7);
        }

        [Fact]
        public void DeploymentSettingsNullWhenKeyDoesNotExist()
        {
            Assert.Equal(null, deploymentSettingsManager.GetValue("key8"));
        }

        private void AssertSetting(int expectedPriority, int key)
        {
            string expectedValue = String.Format("settingsPriority{0}_value{1}", expectedPriority, key);
            Assert.Equal(expectedValue, deploymentSettingsManager.GetValue("key" + key));
        }
    }
}
