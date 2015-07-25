using System;
using System.Collections.Generic;

namespace Kudu.Contracts.Jobs
{
    public class JobSettings : Dictionary<string, object>
    {
        public const string JobSettingsFileName = "settings.job";

        public T GetSetting<T>(string key, T defaultValue = default(T))
        {
            object value;

            if (TryGetValue(key, out value) && value is T)
            {
                return (T)value;
            }

            return defaultValue;
        }

        public void SetSetting(string key, object value)
        {
            this[key] = value;
        }

        public bool IsSingleton
        {
            get
            {
                return GetSetting(JobSettingsKeys.IsSingleton, false);
            }
        }
        public TimeSpan GetStoppingWaitTime(long defaultTime)
        {
            return TimeSpan.FromSeconds(GetSetting(JobSettingsKeys.StoppingWaitTime, defaultTime));
        }

        public bool GetIsInPlace(bool defaultValue)
        {
            return GetSetting(JobSettingsKeys.IsInPlace, defaultValue);
        }

        public string GetSchedule()
        {
            return GetSetting<string>(JobSettingsKeys.Schedule);
        }

    }
}
