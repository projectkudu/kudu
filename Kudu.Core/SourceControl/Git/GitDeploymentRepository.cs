using System.IO;
using System.Linq;
using Kudu.Contracts.Tracing;
using Kudu.Core.Tracing;

namespace Kudu.Core.SourceControl.Git
{
    public class GitDeploymentRepository : IDeploymentRepository
    {
        private readonly string _path;
        private readonly ITraceFactory _traceFactory;
        private readonly GitExeRepository _repository;

        public GitDeploymentRepository(string path, string homePath, ITraceFactory traceFactory)
        {
            _path = path;
            _traceFactory = traceFactory;
            _repository = new GitExeRepository(path, homePath, traceFactory);
        }

        private string PostReceiveHookPath
        {
            get
            {
                return Path.Combine(_path, ".git", "hooks", "post-receive");
            }
        }

        private string PushInfoPath
        {
            get
            {
                return Path.Combine(_path, ".git", "pushinfo");
            }
        }

        public void Clean()
        {
            _repository.Clean();
        }

        public ChangeSet GetChangeSet(string id)
        {
            return _repository.GetChangeSet(id);
        }

        public ReceiveInfo GetReceiveInfo()
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

                // When a branch gets deleted, the newId is an all-zero string.
                // In those cases, we never want to do anything, so return null
                if (newId.Trim('0').Length == 0)
                {
                    return null;
                }

                string reference = pushDetails[2];
                string branch = reference.Split('/').Last().Trim();
                string fullNewId = _repository.Resolve(branch);

                return new ReceiveInfo
                {
                    OldId = oldId,
                    NewId = newId,
                    Branch = new GitBranch(fullNewId, branch, false)
                };
            }

            return null;
        }

        public void Update()
        {
            EnsureNoLockFile();
            _repository.Update();
        }

        public void Update(string id)
        {
            EnsureNoLockFile();
            _repository.Update(id);
        }

        public void UpdateSubmodules()
        {
            EnsureNoLockFile();
            _repository.UpdateSubmodules();
        }

        private void EnsureNoLockFile()
        {
            // Delete the lock file from the .git folder
            string lockFilePath = Path.Combine(_path, ".git", "index.lock");
            if (File.Exists(lockFilePath))
            {
                ITracer tracer = _traceFactory.GetTracer();
                tracer.TraceWarning("Deleting left over index.lock file");
                File.Delete(lockFilePath);
            }

        }
    }
}
