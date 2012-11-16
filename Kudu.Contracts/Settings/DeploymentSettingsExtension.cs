using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Kudu.Contracts.Settings
{
    public static class DeploymentSettingsExtension
    {
        public const TraceLevel DefaultTraceLevel = TraceLevel.Off;

        public static TraceLevel GetTraceLevel(this IDeploymentSettingsManager settings)
        {
            string value = settings.GetValue(SettingsKeys.TraceLevel);
            int level;
            if (!String.IsNullOrEmpty(value) && Int32.TryParse(value, out level))
            {
                if (level <= (int)TraceLevel.Off)
                {
                    return TraceLevel.Off;
                }
                else if (level >= (int)TraceLevel.Verbose)
                {
                    return TraceLevel.Verbose;
                }
                else
                {
                    return (TraceLevel)level;
                }
            }

            return DeploymentSettingsExtension.DefaultTraceLevel;
        }
    }
}