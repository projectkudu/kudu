using System;
using System.Collections.Generic;

namespace Kudu.Contracts.Jobs
{
    public class JobSettings : Dictionary<string, object>
    {
        public T GetSetting<T>(string key, T defaultValue = default(T))
        {
            object value;

            if (TryGetValue(key, out value))
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

        public TimeSpan GetStoppingWaitTime(int defaultTime)
        {
            return TimeSpan.FromSeconds(GetSetting(JobSettingsKeys.StoppingWaitTime, defaultTime));
        }
    }
}
