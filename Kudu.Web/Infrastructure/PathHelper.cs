using System.IO;
using System.Web;

namespace Kudu.Web.Infrastructure {
    internal static class PathHelper {
        // Hard code the path to the services site (makes it easier to debug)
        internal static readonly string ServiceSitePath = Path.GetFullPath(Path.Combine(HttpRuntime.AppDomainAppPath, "..", "Kudu.Services.Web"));
        internal static readonly string RootPath = Path.GetFullPath(Path.Combine(ServiceSitePath, "..", "apps"));

        internal static string GetApplicationPath(string applicationName) {
            return Path.Combine(RootPath, "live", applicationName);
        }

        internal static string GetDeveloperApplicationPath(string applicationName) {
            return Path.Combine(RootPath, "dev", applicationName);
        }

        internal static string GetRepositoryPath(string applicationName) {
            return Path.Combine(GetApplicationPath(applicationName), "repository");
        }
    }
}