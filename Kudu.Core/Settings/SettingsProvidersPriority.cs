using System;

namespace Kudu.Core.Settings
{
    /// <summary>
    /// Settings providers priorities where the higher has higher priority and will override values in lower priorities
    /// </summary>
    public enum SettingsProvidersPriority
    {
        Default = 0,
        PerDeploymentDefault = 3,
        PerDeployment = 5,
        Environment = 10,
        PerSite = 100
    }
}
