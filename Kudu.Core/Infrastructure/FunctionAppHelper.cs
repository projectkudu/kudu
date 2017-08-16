using System.IO;

namespace Kudu.Core.Infrastructure
{
    internal static class FunctionAppHelper
    {
        const string hostJson = "host.json";
        public static bool LooksLikeFunctionApp(string projectFolder)
        {
            // we look for host.json and make sure its a function web app
            // if one of the two conditions is not met, user probably don't want to deploy this PROJECT
            return FileSystemHelpers.FileExists(Path.Combine(projectFolder, hostJson)) &&
                    !string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable(Constants.FunctionRunTimeVersion));
        }
    }
}
