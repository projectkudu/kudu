namespace Kudu.Core.Infrastructure
{
    internal static class FunctionAppHelper
    {
        public static bool LooksLikeFunctionApp()
        {
            return !string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable(Constants.FunctionRunTimeVersion));
        }
    }
}
