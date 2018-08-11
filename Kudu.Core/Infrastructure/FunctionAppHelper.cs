using System;
using System.Linq;

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
            return VsHelper.IncludesAnyReferencePackage(projectPath, "Microsoft.NET.Sdk.Functions");
        }

        public static void ThrowsIfVersionMismatch(string projectPath)
        {
            // "<AzureFunctionsVersion>v2</AzureFunctionsVersion>" exists in v2 csproj
            var properties = VsHelper.GetPropertyValues(projectPath, "AzureFunctionsVersion", VsHelper.Csproj.newFormat);
            var projectMajorVersion = String.Equals(properties.FirstOrDefault(), "v2", StringComparison.OrdinalIgnoreCase) ? FuncVersion.V2 : FuncVersion.V1;
            var runtimeMajorVersion = GetFunctionRuntimeMajorVersion();

            if (runtimeMajorVersion == projectMajorVersion)
            {
                // check succeeded
                return;
            }

            throw new InvalidOperationException($@"Your function app is targeting {projectMajorVersion}, but Azure host has function version {runtimeMajorVersion}, 
please change the version using the portal or update your 'FUNCTIONS_EXTENSION_VERSION' appsetting and retry");
        }

        public static FuncVersion GetFunctionRuntimeMajorVersion()
        {
            // startswith "1." or is "~1" => function v1
            // else => function v2
            var environmentValue = System.Environment.GetEnvironmentVariable(Constants.FunctionRunTimeVersion);
            return (environmentValue.StartsWith("1.", StringComparison.OrdinalIgnoreCase)
                || String.Equals(environmentValue, "~1", StringComparison.OrdinalIgnoreCase))
                ? FuncVersion.V1 : FuncVersion.V2;
        }

        public enum FuncVersion { V1, V2 };
    }
}
