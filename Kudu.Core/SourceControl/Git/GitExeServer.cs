using System.IO;
using System.Linq;
using Kudu.Contracts;
using Kudu.Core.Infrastructure;
using Kudu.Core.Performance;

namespace Kudu.Core.SourceControl.Git
{
    public class GitExeServer : IGitServer, IServerRepository
    {
        private readonly GitExecutable _gitExe;
        private readonly IProfilerFactory _profilerFactory;
        private readonly GitExeRepository _repository;
        
        public GitExeServer(string path, IProfilerFactory profilerFactory)
        {
            _gitExe = new GitExecutable(path);
            _gitExe.SetTraceLevel(2);
            _profilerFactory = profilerFactory;
            _repository = new GitExeRepository(path, profilerFactory);
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

        public void AdvertiseReceivePack(Stream output)
        {
            IProfiler profiler = _profilerFactory.GetProfiler();
            using (profiler.Step("GitExeServer.AdvertiseReceivePack"))
            {
                Advertise(profiler, "receive-pack", output);
            }
        }

        public void AdvertiseUploadPack(Stream output)
        {
            IProfiler profiler = _profilerFactory.GetProfiler();
            using (profiler.Step("GitExeServer.AdvertiseUploadPack"))
            {
                Advertise(profiler, "upload-pack", output);
            }
        }

        public void Clean()
        {
            _repository.Clean();
        }

        public bool Receive(Stream inputStream, Stream outputStream)
        {
            IProfiler profiler = _profilerFactory.GetProfiler();
            using (profiler.Step("GitExeServer.Receive"))
            {
                // Remove the push info path
                FileSystemHelpers.DeleteFileSafe(PushInfoPath);

                ServiceRpc(profiler, "receive-pack", inputStream, outputStream);
            }

            // If out file was written to disk then the push is complete
            return File.Exists(PushInfoPath);
        }

        public void Upload(Stream inputStream, Stream outputStream)
        {
            IProfiler profiler = _profilerFactory.GetProfiler();
            using (profiler.Step("GitExeServer.Upload"))
            {
                ServiceRpc(profiler, "upload-pack", inputStream, outputStream);
            }
        }

        private void Advertise(IProfiler profiler, string serviceName, Stream output)
        {
            _gitExe.Execute(profiler, null, output, @"{0} --stateless-rpc --advertise-refs ""{1}""", serviceName, _gitExe.WorkingDirectory);
        }

        private void ServiceRpc(IProfiler profiler, string serviceName, Stream input, Stream output)
        {
            _gitExe.Execute(profiler, input, output, @"{0} --stateless-rpc ""{1}""", serviceName, _gitExe.WorkingDirectory);
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
            IProfiler profiler = _profilerFactory.GetProfiler();
            using (profiler.Step("GitExeServer.Initialize"))
            {
                _repository.Initialize();

                using (profiler.Step("Configure git server"))
                {
                    // Allow getting pushes even though we're not bare
                    _gitExe.Execute(profiler, "config receive.denyCurrentBranch ignore");
                }

                using (profiler.Step("Setup post receive hook"))
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
