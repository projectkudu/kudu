namespace Kudu.Core.Deployment
{
    public interface IDeploymentEnvironment
    {
        /// <summary>
        /// Path to kudu.exe
        /// </summary>
        string ExePath { get; }

        /// <summary>
        /// Represents _app path in the kudu service (the target application)
        /// </summary>
        string ApplicationPath { get; }

        /// <summary>
        /// Path to the msbuild extension path (contains wap targets etc)
        /// </summary>
        string MSBuildExtensionsPath { get; }
    }
}
