using System;
using System.IO;
using System.Threading.Tasks;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Deployment
{
    public class CustomBuilder : ISiteBuilder
    {
        private readonly string _targetFile;
        private readonly string _repositoryPath;

        private const string SourcePath = "SOURCE";
        private const string TargetPath = "TARGET";
        private const string DeployCmd = "deploy.cmd";

        public CustomBuilder(string repositoryPath, string targetFile)
        {
            _repositoryPath = repositoryPath;
            _targetFile = targetFile;
        }

        public Task Build(DeploymentContext context)
        {
            var tcs = new TaskCompletionSource<object>();

            ILogger customLogger = context.Logger.Log("Running custom deployment..");

            Executable exe = GetExecutable();
            exe.EnvironmentVariables[SourcePath] = _repositoryPath;
            exe.EnvironmentVariables[TargetPath] = context.OutputPath;

            try
            {
                string output = exe.ExecuteWithConsoleOutput(context.Tracer, String.Empty).Item1;

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

        public static bool TryGetCustomDeploymentFile(string repositoryRoot, out string customDeploymentFile)
        {
            string targetFile = Path.Combine(repositoryRoot, DeployCmd);
            customDeploymentFile = null;

            if (File.Exists(targetFile))
            {
                customDeploymentFile = targetFile;
                return true;
            }

            return false;
        }

        private Executable GetExecutable()
        {
            // Creates an executable poiting to Deploy.cmd and the working directory being
            // the repository root
            return new Executable(_targetFile, _repositoryPath);
        }
    }
}
