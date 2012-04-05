using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.SourceControl;
using Kudu.Contracts.Tracing;
using Kudu.Core.Deployment;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;

namespace Kudu.Core.SourceControl.Git
{
    public class GitExeServer : IGitServer, IServerRepository
    {
        private readonly GitExecutable _gitExe;
        private readonly ITraceFactory _traceFactory;
        private readonly GitExeRepository _repository;
        private readonly IOperationLock _initLock;
        private readonly IDeploymentCommandGenerator _deploymentCommandGenerator;

        private static readonly TimeSpan _initTimeout = TimeSpan.FromMinutes(8);

        public GitExeServer(string path, IOperationLock initLock, IDeploymentCommandGenerator deploymentCommandGenerator, ITraceFactory traceFactory)
        {
            _gitExe = new GitExecutable(path);
            _gitExe.SetTraceLevel(2);
            _traceFactory = traceFactory;
            _repository = new GitExeRepository(path, traceFactory);
            _repository.SetTraceLevel(2);
            _initLock = initLock;
            _deploymentCommandGenerator = deploymentCommandGenerator;
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

        public void Clean()
        {
            _repository.Clean();
        }

        public bool Exists
        {
            get
            {
                return Directory.Exists(_gitExe.WorkingDirectory) &&
                       Directory.EnumerateFileSystemEntries(_gitExe.WorkingDirectory).Any();
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

        public ChangeSet Initialize(RepositoryConfiguration configuration, string path)
        {
            if (Exists && !_initLock.IsHeld)
            {
                // Repository already exists and there's nothing happening then do nothing
                return null;
            }

            ChangeSet changeSet = null;

            _initLock.LockOrWait(() =>
            {
                InitializeRepository(configuration);

                ITracer tracer = _traceFactory.GetTracer();
                using (tracer.Step("GitExeServer.Initialize(path)"))
                {
                    tracer.Trace("Initializing repository from path", new Dictionary<string, string>
                    {
                        { "path", path }
                    });

                    // Copy all of the files into the repository
                    FileSystemHelpers.Copy(path, _gitExe.WorkingDirectory);

                    // Make the initial commit
                    changeSet = _repository.Commit("Initial commit");

                }
            },
            _initTimeout);

            return changeSet;
        }

        public bool Initialize(RepositoryConfiguration configuration)
        {
            if (Exists && !_initLock.IsHeld)
            {
                // Repository already exists and there's nothing happening then do nothing
                return false;
            }

            _initLock.LockOrWait(() => InitializeRepository(configuration), _initTimeout);

            return true;
        }

        private void InitializeRepository(RepositoryConfiguration configuration)
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

                using (tracer.Step("Configure git user and email"))
                {
                    _gitExe.Execute(tracer, @"config user.name ""{0}""", configuration.Username);
                    _gitExe.Execute(tracer, @"config user.email ""{0}""", configuration.Email);
                }

                using (tracer.Step("Setup post receive hook"))
                {
                    FileSystemHelpers.EnsureDirectory(Path.GetDirectoryName(PostReceiveHookPath));

                    string content = @"#!/bin/sh
read i
echo $i > pushinfo
";
                    string command = "\n" + _deploymentCommandGenerator.GetDeploymentCommand();

                    File.WriteAllText(PostReceiveHookPath, content + command);
                }
            }
        }

        public RepositoryType GetRepositoryType()
        {
            return RepositoryType.Git;
        }
    }
}
