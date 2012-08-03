using System;
using System.IO;
using System.Threading.Tasks;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Deployment
{
    public class CustomBuilder : ISiteBuilder
    {
        private readonly string _command;
        private readonly string _repositoryPath;
        private readonly IBuildPropertyProvider _propertyProvider;

        private const string SourcePath = "SOURCE";
        private const string TargetPath = "TARGET";

        public CustomBuilder(string repositoryPath, string command, IBuildPropertyProvider propertyProvider)
        {
            _repositoryPath = repositoryPath;
            _command = command;
            _propertyProvider = propertyProvider;
        }

        public Task Build(DeploymentContext context)
        {
            var tcs = new TaskCompletionSource<object>();

            ILogger customLogger = context.Logger.Log("Running custom deployment...");

            // Creates an executable pointing to cmd and the working directory being
            // the repository root
            var exe = new Executable("cmd", _repositoryPath);
            exe.EnvironmentVariables[SourcePath] = _repositoryPath;
            exe.EnvironmentVariables[TargetPath] = context.OutputPath;

            // Populate the enviornment with the build propeties
            foreach (var property in _propertyProvider.GetProperties())
            {
                exe.EnvironmentVariables[property.Key] = property.Value;
            }

            // Set the path so we can add more variables
            exe.EnvironmentVariables["PATH"] = System.Environment.GetEnvironmentVariable("PATH");

            // Add the msbuild path and git path to the %PATH% so more tools are available
            var toolsPaths = new[] {
                Path.GetDirectoryName(PathUtility.ResolveMSBuildPath()),
                Path.GetDirectoryName(PathUtility.ResolveGitPath())
            };

            exe.AddToPath(toolsPaths);

            try
            {
                string output = exe.ExecuteWithConsoleOutput(context.Tracer, "/c " + _command, String.Empty).Item1;

                customLogger.Log(output);

                tcs.SetResult(null);
            }
            catch (Exception ex)
            {
                context.Tracer.TraceError(ex);

                // HACK: Log an empty error to the global logger (post receive hook console output).
                // The reason we don't log the real exception is because the 'live output' running
                // msbuild has already been captured.
                context.GlobalLogger.LogError();

                customLogger.Log(ex);

                tcs.SetException(ex);
            }

            return tcs.Task;
        }
    }
}
