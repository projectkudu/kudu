using System;
using System.IO;
using System.Web.Hosting;

namespace Kudu.Services.Web
{
    public static class PathResolver
    {
        public static string ResolveRootPath()
        {
            string path = HostingEnvironment.MapPath(Constants.MappedSite);

            // In Azure, the mapped path should not exist and fallback to %HOME%.
            // To minimize regression, only set to HOME path if it exists.
            if (!Directory.Exists(path))
            {
                var homePath = Environment.ExpandEnvironmentVariables(@"%HOME%");
                if (Directory.Exists(homePath))
                {
                    path = homePath;
                }
            }

            return Path.GetFullPath(path);
        }
    }
}