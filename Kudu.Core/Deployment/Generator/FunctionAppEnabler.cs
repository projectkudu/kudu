
using Kudu.Core.Infrastructure;
using System.IO;

namespace Kudu.Core.Deployment.Generator
{
    public static class FunctionAppEnabler
    {
        private const string FunctionAppRootConfig = "host.json";

        public static bool LooksLikeFunctionApp(string siteFolder)
        {
            return FileSystemHelpers.FileExists(Path.Combine(siteFolder, FunctionAppRootConfig));
        }
           
    }
}
