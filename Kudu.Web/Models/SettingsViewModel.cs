using System.Collections.Generic;
using Kudu.Core.Deployment;

namespace Kudu.Web.Models
{
    public class SettingsViewModel
    {
        public IEnumerable<DeploymentSetting> AppSettings { get; set; }
        public IEnumerable<ConnectionStringSetting> ConnectionStrings { get; set; }
        public ApplicationViewModel Application { get; set; }
        public bool Enabled { get; set; }
    }
}