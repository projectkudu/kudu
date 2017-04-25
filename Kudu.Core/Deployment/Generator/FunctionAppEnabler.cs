namespace Kudu.Core.Deployment.Generator
{
    public static class FunctionAppEnabler
    {

        public static bool LooksLikeFunctionApp()
        {
            return !string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable(Constants.FunctionRunTimeVersion));
        }

    }
}
