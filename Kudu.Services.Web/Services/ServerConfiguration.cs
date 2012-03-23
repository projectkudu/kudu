using System;
using System.Web;

namespace Kudu.Services.Web
{
    public class ServerConfiguration : IServerConfiguration
    {
        public string ApplicationName
        {
            get
            {
                return Environment.GetEnvironmentVariable("APP_POOL_ID");
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
    }
}