using System;
using System.IO;
using System.Web.Hosting;

namespace Kudu.Services.Web
{
    public static class PathResolver
    {
        public static string ResolveRootPath()
        {
            return Path.GetFullPath(ResolveRootPathInternal());
        }

        private static string ResolveRootPathInternal()
        {
            // If MapPath("/app") returns a valid folder, use it. This is the non-Azure code path
            string path = HostingEnvironment.MapPath(Constants.MappedSite);
            if (Directory.Exists(path))
            {
                return path;
            }

            // If d:\home exists, use it. This is a 'magic' folder on Azure that points to the root of the site files
            // However, to avoid a bug on Azure, don't do this for legacy sites that have a d:\home\site\.ssh folder
            // Note that new sites should have their .ssh folder as d:\home\.ssh
            path = Environment.ExpandEnvironmentVariables(@"%SystemDrive%\home");
            if (Directory.Exists(path) &&
                !Directory.Exists(Path.Combine(path, Constants.SiteFolder, Constants.SSHKeyPath)))
            {
                return path;
            }

            // Fall back to the HOME env variable, which is set on Azure but to a longer folder that looks
            // something like C:\DWASFiles\Sites\MySite\VirtualDirectory0
            path = Environment.ExpandEnvironmentVariables(@"%HOME%");
            if (Directory.Exists(path))
            {
                return path;
            }

            // We should never get here
            throw new DirectoryNotFoundException("The site's home directory could not be located");
        }
    }
}