using System;

namespace Kudu.Core.Infrastructure
{
    public class ServerConfiguration : IServerConfiguration
    {
        private string _applicationName;

        public string ApplicationName
        {
            get
            {
                if (_applicationName == null)
                {
                    _applicationName = GetApplicationName();
                }
                return _applicationName;
            }
        }

        public string GitServerRoot
        {
            get
            {
                if (String.IsNullOrEmpty(ApplicationName))
                {
                    return "git";
                }
                return ApplicationName + ".git";
            }
        }

        public static string GetApplicationName()
        {
            var applicationName = System.Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME");
            if(!string.IsNullOrEmpty(applicationName))
            {
                // Yank everything after the first underscore to work around
                // a slot issue where WEBSITE_SITE_NAME gets set incorrectly
                int underscoreIndex = applicationName.IndexOf('_');
                if (underscoreIndex > 0)
                {
                    applicationName = applicationName.Substring(0, underscoreIndex);
                }

                return applicationName;
            }

            applicationName = System.Environment.GetEnvironmentVariable("APP_POOL_ID");
            if (applicationName != null)
            {
                return applicationName;
            }

            return String.Empty;
        }
    }
}
