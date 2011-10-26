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
                return HttpRuntime.AppDomainAppVirtualPath.Trim('/');
            }
        }

        public string HgServerRoot
        {
            get
            {
                if (String.IsNullOrEmpty(ApplicationName))
                {
                    return "hg";
                }
                return ApplicationName;
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