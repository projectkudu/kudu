using System;
using System.Diagnostics;
using System.IO;
using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.Settings;
using Kudu.Contracts.SourceControl;
using Kudu.Contracts.Tracing;
using Kudu.Core.Deployment;
using Kudu.Core.Tracing;

namespace Kudu.Core.SourceControl.Git
{
    public class GitExeServer : IGitServer
    {
        public static readonly TimeSpan InitTimeout = TimeSpan.FromMinutes(8);
        private readonly GitExecutable _gitExe;
        private readonly ITraceFactory _traceFactory;
        private readonly IOperationLock _initLock;
        private readonly IRepositoryFactory _repositoryFactory;

        public GitExeServer(IEnvironment environment,
                            IOperationLock initLock, 
                            string logFileEnv,
                            IRepositoryFactory repositoryFactory,
                            IDeploymentEnvironment deploymentEnvironment, 
                            IDeploymentSettingsManager settings, 
                            ITraceFactory traceFactory)
        {
            _gitExe = new GitExecutable(environment.RepositoryPath, settings.GetCommandIdleTimeout());
            _gitExe.SetHomePath(environment);
            _traceFactory = traceFactory;
            _initLock = initLock;
            _repositoryFactory = repositoryFactory;

            // Transfer logFileEnv => git.exe => kudu.exe, this represent per-request tracefile
            _gitExe.EnvironmentVariables[Constants.TraceFileEnvKey] = logFileEnv;

            // Setup the deployment environment variable to be used by the post receive hook
            _gitExe.EnvironmentVariables[KnownEnvironment.EXEPATH] = deploymentEnvironment.ExePath;
            _gitExe.EnvironmentVariables[KnownEnvironment.APPPATH] = deploymentEnvironment.ApplicationPath;
            _gitExe.EnvironmentVariables[KnownEnvironment.MSBUILD] = deploymentEnvironment.MSBuildExtensionsPath;
            _gitExe.EnvironmentVariables[KnownEnvironment.DEPLOYER] = "";
        }

        public void AdvertiseReceivePack(Stream output)
        {
            Initialize();

            ITracer tracer = _traceFactory.GetTracer();
            using (tracer.Step("GitExeServer.AdvertiseReceivePack"))
            {
                Advertise(tracer, "receive-pack", output);
            }
        }

        public void AdvertiseUploadPack(Stream output)
        {
            Initialize();

            ITracer tracer = _traceFactory.GetTracer();
            using (tracer.Step("GitExeServer.AdvertiseUploadPack"))
            {
                Advertise(tracer, "upload-pack", output);
            }
        }

        public void Receive(Stream inputStream, Stream outputStream)
        {
            ITracer tracer = _traceFactory.GetTracer();
            using (tracer.Step("GitExeServer.Receive"))
            {
                ServiceRpc(tracer, "receive-pack", inputStream, outputStream);
            }
        }

        public void Upload(Stream inputStream, Stream outputStream)
        {
            ITracer tracer = _traceFactory.GetTracer();
            using (tracer.Step("GitExeServer.Upload"))
            {
                ServiceRpc(tracer, "upload-pack", inputStream, outputStream);
            }
        }

        private void Advertise(ITracer tracer, string serviceName, Stream output)
        {
            _gitExe.Execute(tracer, null, output, @"{0} --stateless-rpc --advertise-refs ""{1}""", serviceName, _gitExe.WorkingDirectory);
        }

        private void ServiceRpc(ITracer tracer, string serviceName, Stream input, Stream output)
        {
            _gitExe.Execute(tracer, input, output, @"{0} --stateless-rpc ""{1}""", serviceName, _gitExe.WorkingDirectory);
        }

        internal void Initialize()
        {
            IRepository repository = _repositoryFactory.GetRepository();
            if (repository == null)
            {
                _initLock.LockOperation(() =>
                {
                    repository = _repositoryFactory.GetRepository();
                    if (repository == null)
                    {
                        InitializeRepository();
                    }
                }, InitTimeout);
            }
        }

        private void InitializeRepository()
        {
            ITracer tracer = _traceFactory.GetTracer();
            using (tracer.Step("GitExeServer.Initialize"))
            {
                _repositoryFactory.EnsureRepository(RepositoryType.Git);
            }
        }

        public void SetDeployer(string deployer)
        {
            _gitExe.EnvironmentVariables[KnownEnvironment.DEPLOYER] = deployer;
        }
    }
}
