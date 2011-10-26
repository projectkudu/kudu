using System.IO;
using System.Web;

namespace Kudu.Services.Web
{
    public static class PathResolver
    {
        public static string ResolveRootPath()
        {
            string path = Path.Combine(HttpRuntime.AppDomainAppPath, "..", "apps", "live");
            return Path.GetFullPath(path);
        }

        public static string ResolveDevelopmentPath()
        {
            string path = Path.Combine(HttpRuntime.AppDomainAppPath, "..", "apps", "dev");
            if (!Directory.Exists(path))
            {
                return null;
            }
            return Path.GetFullPath(path);
        }
    }
}