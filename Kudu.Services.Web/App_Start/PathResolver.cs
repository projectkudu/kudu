using System.IO;
using System.Web.Hosting;

namespace Kudu.Services.Web
{
    public static class PathResolver
    {
        public static string ResolveSiteRootPath()
        {
            string path = HostingEnvironment.MapPath(Constants.MappedSite);
            return Path.GetFullPath(path);
        }
    }
}