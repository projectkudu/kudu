using System.IO;
using System.Web.Hosting;

namespace Kudu.Services.Web
{
    public static class PathResolver
    {
        public static string ResolveRootPath()
        {
            string path = HostingEnvironment.MapPath(Constants.MappedLiveSite);
            return Path.GetFullPath(path);
        }

        public static string ResolveDevelopmentPath()
        {
            string path = HostingEnvironment.MapPath(Constants.MappedDevSite);
            if (!Directory.Exists(path))
            {
                // Temporary workaround until MapPath("/_devapp") is fixed
                path = Path.Combine(ResolveRootPath(), @"dev_wwwroot");
                if (!Directory.Exists(path))
                {
                    return null;
                }
            }
            return Path.GetFullPath(path);
        }
    }
}