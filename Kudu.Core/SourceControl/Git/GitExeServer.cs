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
        private static readonly TimeSpan _initTimeout = TimeSpan.FromMinutes(8);

        // Server git operations like receive-pack can take a long time for large repros, without any data flowing.
        // So use a long 30 minute timeout here instead of the much shorter default.
        private static readonly TimeSpan _gitMinTimeout = TimeSpan.FromMinutes(30);

        private readonly GitExecutable _gitExe;
        private readonly ITraceFactory _traceFactory;
        private readonly IOperationLock _initLock;
        private readonly IRepositoryFactory _repositoryFactory;

        public GitExeServer(string path, 
                            string homePath, 
                            IOperationLock initLock, 
                            string logFileEnv,
                            IRepositoryFactory repositoryFactory,
                            IDeploymentEnvironment deploymentEnvironment, 
                            IDeploymentSettingsManager settings, 
                            ITraceFactory traceFactory)
        {
            // Honor settings if longer
            var gitTimeout = settings.GetCommandIdleTimeout();
            if (gitTimeout < _gitMinTimeout)
            {
                gitTimeout = _gitMinTimeout;
            }

            _gitExe = new GitExecutable(path, gitTimeout);
            _gitExe.SetHomePath(homePath);
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

        private bool Initialize()
        {
            IRepository repository = _repositoryFactory.GetRepository();
            if (repository != null && !_initLock.IsHeld)
            {
                // Repository already exists and there's nothing happening then do nothing
                Debug.Assert(repository.RepositoryType == RepositoryType.Git, "Should be a higher order check that we're not affecting an existing Mercurial repo");
                return false;
            }

            _initLock.LockOrWait(() => InitializeRepository(), _initTimeout);

            return true;
        }

        private void InitializeRepository()
        {
            ITracer tracer = _traceFactory.GetTracer();
            using (tracer.Step("GitExeServer.Initialize"))
            {
                IRepository repository = _repositoryFactory.EnsureRepository(RepositoryType.Git);

                using (tracer.Step("Configure git server"))
                {
                    // Allow getting pushes even though we're not bare
                    _gitExe.Execute(tracer, "config receive.denyCurrentBranch ignore");
                }
            }
        }

        public void SetDeployer(string deployer)
        {
            _gitExe.EnvironmentVariables[KnownEnvironment.DEPLOYER] = deployer;
        }
    }
}
