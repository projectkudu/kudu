using System;
using System.Collections.Generic;
using System.Linq;

namespace Kudu.Core.Infrastructure
{
    public static class FunctionAppHelper
    {
        private static string _functionRunTimeVersion;

        public static string FunctionRunTimeVersion
        {
            get
            {
                if (!string.IsNullOrEmpty(_functionRunTimeVersion))
                {
                    return _functionRunTimeVersion;
                }

                return System.Environment.GetEnvironmentVariable(Constants.FunctionRunTimeVersion);
            }
            set
            {
                _functionRunTimeVersion = value;
            }
        }

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
            ThrowsIfVersionMismatch(VsHelper.GetPropertyValues(projectPath, "AzureFunctionsVersion", VsHelper.Csproj.newFormat));
        }

        public static void ThrowsIfVersionMismatch(IEnumerable<string> properties)
        {
            // "<AzureFunctionsVersion>v2</AzureFunctionsVersion>" exists in v2 csproj
            var projectMajorVersion = GetProjectMajorVersion(properties);
            var runtimeMajorVersion = GetFunctionRuntimeMajorVersion();

            if (projectMajorVersion <= 0 || runtimeMajorVersion <= 0)
            {
                return;
            }

            if (runtimeMajorVersion == projectMajorVersion)
            {
                // check succeeded
                return;
            }

            throw new InvalidOperationException($@"Your function app is targeting {properties.FirstOrDefault()}, but Azure host has function version {FunctionRunTimeVersion}, 
please change the version using the portal or update your 'FUNCTIONS_EXTENSION_VERSION' appsetting and retry");
        }

        public static int GetProjectMajorVersion(IEnumerable<string> properties)
        {
            var property = properties.FirstOrDefault();
            if (string.IsNullOrEmpty(property))
            {
                return 0;
            }

            return int.TryParse(property.TrimStart('v').Split('.', '-').FirstOrDefault(), out int version) ? version : 0;
        }

        public static int GetFunctionRuntimeMajorVersion()
        {
            var environmentValue = FunctionRunTimeVersion;
            if (string.IsNullOrEmpty(environmentValue))
            {
                return 0;
            }

            return int.TryParse(environmentValue.TrimStart('~').Split('.', '-').FirstOrDefault(), out int version) ? version : 0;
        }
    }
}
