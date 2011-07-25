using System.Web;

namespace Kudu.Services.Web {
    public class DefaultServerConfiguration : IServerConfiguration {
        public string ApplicationName {
            get {
                return HttpRuntime.AppDomainAppVirtualPath.Trim('/');
            }
        }

        public string HgServerRoot {
            get {
                return ApplicationName;
            }
        }

        public string GitServerRoot {
            get {
                return ApplicationName + ".git";
            }
        }
    }
}