using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq.Expressions;
using Kudu.Contracts.Settings;

namespace Kudu.Web.Models
{
    public class DeploymentSettingsViewModel
    {
        public string Branch { get; set; }

        public string BuildArgs { get; set; }

        public IDictionary<string, string> SiteSettings { get; set; }

        public DeploymentSettingsViewModel()
            : this(null)
        {
        }

        public DeploymentSettingsViewModel(NameValueCollection kuduSettings)
        {
            SiteSettings = new Dictionary<string, string>();
            if (kuduSettings != null)
            {
                foreach (var key in kuduSettings.AllKeys)
                {
                    if (SettingsKeys.Branch.Equals(key))
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