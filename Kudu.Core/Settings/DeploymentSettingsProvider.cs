using System.Collections.Generic;
using System.IO;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Settings
{
    public class DeploymentSettingsProvider : BasicSettingsProvider
    {
        public const string DeployConfigFile = ".deployment";

        public DeploymentSettingsProvider(string path)
            : base(GetValues(path), SettingsProvidersPriority.PerDeployment)
        {
        }

        private static IDictionary<string, string> GetValues(string path)
        {
            var iniFile = new IniFile(Path.Combine(path, DeployConfigFile));
            var values = iniFile.GetSectionValues("config");
            if (values == null)
            {
                values = new Dictionary<string, string>();
            }
            return values;
        }
    }
}
