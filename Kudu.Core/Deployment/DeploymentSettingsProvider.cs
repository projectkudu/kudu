using System.Collections.Generic;
using System.Linq;
using XmlSettings;

namespace Kudu.Core.Deployment {
    public class DeploymentSettingsProvider : IDeploymentSettingsProvider {
        private readonly ISettings _settings;

        public DeploymentSettingsProvider(ISettings settings) {
            _settings = settings;
        }

        public IEnumerable<DeploymentSetting> GetAppSettings() {
            return GetSettings("appSettings");
        }

        public IEnumerable<DeploymentSetting> GetConnectionStrings() {
            return GetSettings("connectionStrings");
        }

        private IEnumerable<DeploymentSetting> GetSettings(string sectionName) {
            var section = _settings.GetValues(sectionName);

            if (section != null) {
                return section.Select(pair => new DeploymentSetting {
                    Key = pair.Key,
                    Value = pair.Value
                });
            }

            return null;
        }
    }
}
