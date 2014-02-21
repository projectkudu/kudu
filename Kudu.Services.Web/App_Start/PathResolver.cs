using System;
using System.IO;
using System.Web.Hosting;

namespace Kudu.Services.Web
{
    public static class PathResolver
    {
        public static string ResolveRootPath()
        {
            // The HOME path should always be set correctly
            string path = Environment.ExpandEnvironmentVariables(@"%HOME%");
            if (Directory.Exists(path))
            {
                return path;
            }

            // We should never get here
            throw new DirectoryNotFoundException("The site's home directory could not be located");
        }
    }
}