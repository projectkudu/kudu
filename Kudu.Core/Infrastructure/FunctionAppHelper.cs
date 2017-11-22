namespace Kudu.Core.Infrastructure
{
    internal static class FunctionAppHelper
    {
        public static bool LooksLikeFunctionApp()
        {
            return !string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable(Constants.FunctionRunTimeVersion));
        }

        public static bool IsCSharpFunctionFromProjectFile(string projectPath)
        {
            return VsHelper.IncludesReferencePackage(projectPath, "Microsoft.NET.Sdk.Functions");
        }

    }
}
