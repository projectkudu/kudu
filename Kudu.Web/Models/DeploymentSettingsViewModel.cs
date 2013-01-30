using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using Kudu.Contracts.Settings;

namespace Kudu.Web.Models
{
    public class DeploymentSettingsViewModel
    {
        // these keys are not allowed to be added as a custom property, this is validated by the controller
        public static readonly string[] ReservedSettingKeys = new[] { SettingsKeys.DeploymentBranch, SettingsKeys.BuildArgs };
        
        public string Branch { get; set; }

        public string BuildArgs { get; set; }

        public IDictionary<string, string> SiteSettings { get; set; }

        public DeploymentSettingsViewModel()
            : this(kuduSettings: null)
        {
        }

        public DeploymentSettingsViewModel(NameValueCollection kuduSettings = null)
        {
            SiteSettings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (kuduSettings != null)
            {
                foreach (var key in kuduSettings.AllKeys)
                {
                    if (SettingsKeys.DeploymentBranch.Equals(key))
                    {
                        Branch = kuduSettings.Get(key);
                        continue;
                    }

                    if (SettingsKeys.BuildArgs.Equals(key))
                    {
                        BuildArgs = kuduSettings.Get(key);
                        continue;
                    }

                    SiteSettings[key] = kuduSettings.Get(key);
                }                
            }
        }
    }
}