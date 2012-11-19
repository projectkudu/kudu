using System.Collections.Specialized;

namespace Kudu.Web.Models
{
    public class SettingsViewModel
    {
        public NameValueCollection AppSettings { get; set; }
        public NameValueCollection ConnectionStrings { get; set; }
        public DeploymentSettingsViewModel KuduSettings { get; set; }
        public bool Enabled { get; set; }
    }
}