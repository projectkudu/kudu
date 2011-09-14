using System;
using System.Collections.Generic;

namespace Kudu.Core.Deployment {
    public interface IDeploymentSettingsProvider {
        IEnumerable<DeploymentSetting> GetAppSettings();
        IEnumerable<DeploymentSetting> GetConnectionStrings();
    }
}
