using System.Diagnostics;

namespace Kudu.Core.Infrastructure
{
    public static class InstanceIdUtility
    {
        private static string _instanceId;
        private static string _shortInstanceId;

        public static string GetInstanceId()
        {
            EnsureInstanceId();
            return _instanceId;
        }

        public static string GetShortInstanceId()
        {
            EnsureInstanceId();
            return _shortInstanceId;
        }

        private static void EnsureInstanceId()
        {
            if (_instanceId != null)
            {
                return;
            }

            string instanceId = System.Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID");
            if (string.IsNullOrEmpty(instanceId))
            {
                instanceId = System.Environment.MachineName;
            }
            _instanceId = instanceId;

            Debug.Assert(_instanceId.Length >= 6);
            _shortInstanceId = _instanceId.Substring(0, 6);
        }
    }
}
