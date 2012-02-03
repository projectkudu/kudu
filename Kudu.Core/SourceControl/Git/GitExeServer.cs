using System;
using System.IO;
using System.Linq;
using Kudu.Contracts;
using Kudu.Core.Infrastructure;
using Kudu.Core.Performance;

namespace Kudu.Core.SourceControl.Git
{
    public class GitExeServer : IGitServer, IServerRepository
    {
        private readonly Executable _gitExe;
        private readonly IProfilerFactory _profilerFactory;
        private readonly GitExeRepository _repository;
        private readonly Action _initialize;

        public GitExeServer(string path, IProfilerFactory profilerFactory)
            : this(GitUtility.ResolveGitPath(), path, profilerFactory)
        {
        }

        public GitExeServer(string pathToGitExe, string path, IProfilerFactory profilerFactory)
        {
            _gitExe = new Executable(pathToGitExe, path);
            _profilerFactory = profilerFactory;
            _repository = new GitExeRepository(path);
            _initialize = () => new LibGitRepository(path).Initialize();
        }

        private string PostReceiveHookPath
        {
            get
            {
                return Path.Combine(_gitExe.WorkingDirectory, ".git", "hooks", "post-receive");
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
                Advertise("receive-pack", output);
            }
        }

        public void AdvertiseUploadPack(Stream output)
        {
            IProfiler profiler = _profilerFactory.GetProfiler();
            using (profiler.Step("GitExeServer.AdvertiseUploadPack"))
            {
                Advertise("upload-pack", output);
            }
        }

        public void Receive(Stream inputStream, Stream outputStream)
        {
            IProfiler profiler = _profilerFactory.GetProfiler();
            using (profiler.Step("GitExeServer.Receive"))
            {
                ServiceRpc("receive-pack", inputStream, outputStream);
            }
        }

        public void Upload(Stream inputStream, Stream outputStream)
        {
            IProfiler profiler = _profilerFactory.GetProfiler();
            using (profiler.Step("GitExeServer.Upload"))
            {
                ServiceRpc("upload-pack", inputStream, outputStream);
            }
        }

        private void Advertise(string serviceName, Stream output)
        {
            _gitExe.Execute(null, output, @"{0} --stateless-rpc --advertise-refs ""{1}""", serviceName, _gitExe.WorkingDirectory);
        }

        private void ServiceRpc(string serviceName, Stream input, Stream output)
        {
            _gitExe.Execute(input, output, @"{0} --stateless-rpc ""{1}""", serviceName, _gitExe.WorkingDirectory);
        }

        public PushInfo GetPushInfo()
        {
            string path = Path.Combine(_gitExe.WorkingDirectory, ".git", "pushinfo");

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
                // If we already have a repository then do nothing
                RepositoryType repositoryType = RepositoryManager.GetRepositoryType(_gitExe.WorkingDirectory);
                if (repositoryType == RepositoryType.Git)
                {
                    return;
                }

                // Initialize using LibGit2Sharp
                _initialize();

                // Allow getting pushes even though we're not bare
                _gitExe.Execute("config receive.denyCurrentBranch ignore");

                FileSystemHelpers.EnsureDirectory(Path.GetDirectoryName(PostReceiveHookPath));

                File.WriteAllText(PostReceiveHookPath, @"#!/bin/sh
read i
echo $i > pushinfo
echo ""Queued for deployment.""
");
            }
        }

        public ChangeSet GetChangeSet(string id)
        {
            return _repository.GetChangeSet(id);
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
