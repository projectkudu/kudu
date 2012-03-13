using System.IO;
using System.Linq;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;

namespace Kudu.Core.SourceControl.Git
{
    public class GitExeServer : IGitServer, IServerRepository
    {
        private readonly GitExecutable _gitExe;
        private readonly ITraceFactory _traceFactory;
        private readonly GitExeRepository _repository;
        
        public GitExeServer(string path, ITraceFactory traceFactory)
        {
            _gitExe = new GitExecutable(path);
            _gitExe.SetTraceLevel(2);
            _traceFactory = traceFactory;
            _repository = new GitExeRepository(path, traceFactory);
            _repository.SetTraceLevel(2);
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

        public string CurrentId
        {
            get
            {
                return _repository.CurrentId;
            }
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

        public void Clean()
        {
            _repository.Clean();
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

        public PushInfo GetPushInfo()
        {
            string path = PushInfoPath;

            if (!File.Exists(path))
            {
                return null;
            }

            string[] pushDetails = File.ReadAllText(path).Split(' ');

            if (pushDetails.Length == 3)
            {
                string oldId = pushDetails[0];
                string newId = pushDetails[1];
                string reference = pushDetails[2];
                string branch = reference.Split('/').Last().Trim();

                return new PushInfo
                {
                    OldId = oldId,
                    NewId = newId,
                    Branch = new GitBranch(newId, branch, false)
                };
            }

            return null;
        }

        public void Initialize()
        {
            ITracer tracer = _traceFactory.GetTracer();
            using (tracer.Step("GitExeServer.Initialize"))
            {
                if (Exists)
                {
                    // Repository already exists so do nothing
                    return;
                }

                _repository.Initialize();

                using (tracer.Step("Configure git server"))
                {
                    // Allow getting pushes even though we're not bare
                    _gitExe.Execute(tracer, "config receive.denyCurrentBranch ignore");
                }

                using (tracer.Step("Setup post receive hook"))
                {
                    FileSystemHelpers.EnsureDirectory(Path.GetDirectoryName(PostReceiveHookPath));

                    File.WriteAllText(PostReceiveHookPath, @"#!/bin/sh
read i
echo $i > pushinfo
");
                }
            }
        }

        public ChangeSet GetChangeSet(string id)
        {
            return _repository.GetChangeSet(id);
        }

        public RepositoryType GetRepositoryType()
        {
            return RepositoryType.Git;
        }

        public void Update(string id)
        {
            _repository.Update(id);
        }

        public void Update()
        {
            _repository.Update();
        }
    }
}
