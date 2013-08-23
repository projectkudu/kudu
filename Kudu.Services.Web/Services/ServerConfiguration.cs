using System;
using System.Web;

namespace Kudu.Services.Web
{
    public class ServerConfiguration : IServerConfiguration
    {
        string _applicationName;
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

        static private string GetApplicationName()
        {
            var applicationName = Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME");
            if(!string.IsNullOrEmpty(applicationName))
            {
                return applicationName;
            }
            applicationName = Environment.GetEnvironmentVariable("APP_POOL_ID");
            return applicationName;
        }
    }
}
