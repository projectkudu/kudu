using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core.Deployment;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;

namespace Kudu.Core.SourceControl.Git
{
    public class GitExeServer : IGitServer
    {
        private readonly GitExecutable _gitExe;
        private readonly ITraceFactory _traceFactory;
        private readonly GitExeRepository _repository;
        private readonly IOperationLock _initLock;

        private static readonly TimeSpan _initTimeout = TimeSpan.FromMinutes(8);

        public GitExeServer(string path, string homePath, IOperationLock initLock, string logFileEnv, IDeploymentEnvironment deploymentEnvironment, IDeploymentSettingsManager settings, ITraceFactory traceFactory)
        {
            _gitExe = new GitExecutable(path, settings.GetCommandIdleTimeout());
            _gitExe.SetHomePath(homePath);
            _traceFactory = traceFactory;
            _repository = new GitExeRepository(path, homePath, settings, traceFactory);
            _initLock = initLock;

            // Transfer logFileEnv => git.exe => kudu.exe, this represent per-request tracefile
            _gitExe.EnvironmentVariables[Constants.TraceFileEnvKey] = logFileEnv;

            // Setup the deployment environment variable to be used by the post receive hook
            _gitExe.EnvironmentVariables[KnownEnvironment.EXEPATH] = deploymentEnvironment.ExePath;
            _gitExe.EnvironmentVariables[KnownEnvironment.APPPATH] = deploymentEnvironment.ApplicationPath;
            _gitExe.EnvironmentVariables[KnownEnvironment.MSBUILD] = deploymentEnvironment.MSBuildExtensionsPath;
            _gitExe.EnvironmentVariables[KnownEnvironment.DEPLOYER] = "";
        }

        private string PostReceiveHookPath
        {
            get
            {
                return Path.Combine(_gitExe.WorkingDirectory, ".git", "hooks", "post-receive");
            }
        }

        private string PushInfoPath
        {
            get
            {
                return Path.Combine(_gitExe.WorkingDirectory, ".git", "pushinfo");
            }
        }

        public void AdvertiseReceivePack(Stream output)
        {
            ITracer tracer = _traceFactory.GetTracer();
            using (tracer.Step("GitExeServer.AdvertiseReceivePack"))
            {
                Advertise(tracer, "receive-pack", output);
            }
        }

        public void AdvertiseUploadPack(Stream output)
        {
            ITracer tracer = _traceFactory.GetTracer();
            using (tracer.Step("GitExeServer.AdvertiseUploadPack"))
            {
                Advertise(tracer, "upload-pack", output);
            }
        }

        public bool Receive(Stream inputStream, Stream outputStream)
        {
            ITracer tracer = _traceFactory.GetTracer();
            using (tracer.Step("GitExeServer.Receive"))
            {
                // Remove the push info path
                FileSystemHelpers.DeleteFileSafe(PushInfoPath);

                ServiceRpc(tracer, "receive-pack", inputStream, outputStream);
            }

            // If out file was written to disk then the push is complete
            return File.Exists(PushInfoPath);
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

        public bool Initialize()
        {
            if (_repository.Exists && !_initLock.IsHeld)
            {
                // Repository already exists and there's nothing happening then do nothing
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
                _repository.Initialize();

                using (tracer.Step("Configure git server"))
                {
                    // Allow getting pushes even though we're not bare
                    _gitExe.Execute(tracer, "config receive.denyCurrentBranch ignore");
                }

                using (tracer.Step("Setup post receive hook"))
                {
                    FileSystemHelpers.EnsureDirectory(Path.GetDirectoryName(PostReceiveHookPath));

                    string content = @"#!/bin/sh
read i
echo $i > pushinfo
" + KnownEnvironment.KUDUCOMMAND + "\n";

                    File.WriteAllText(PostReceiveHookPath, content);
                }
            }
        }

        public void SetDeployer(string deployer)
        {
            _gitExe.EnvironmentVariables[KnownEnvironment.DEPLOYER] = deployer;
        }

        /// <summary>
        /// Environment variables used for the post receive hook
        /// </summary>
        private static class KnownEnvironment
        {
            public const string EXEPATH = "KUDU_EXE";
            public const string APPPATH = "KUDU_APPPATH";
            public const string MSBUILD = "KUDU_MSBUILD";
            public const string DEPLOYER = "KUDU_DEPLOYER";

            // Command to launch the post receive hook
            public static string KUDUCOMMAND = "\"$" + EXEPATH + "\" " +
                                               "\"$" + APPPATH + "\" " +
                                               "\"$" + MSBUILD + "\" " +
                                               "\"$" + DEPLOYER + "\"";
        }
    }
}
