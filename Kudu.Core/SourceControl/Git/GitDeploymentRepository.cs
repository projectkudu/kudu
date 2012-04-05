using System.IO;
using System.Linq;
using Kudu.Core.Tracing;

namespace Kudu.Core.SourceControl.Git
{
    public class GitDeploymentRepository : IDeploymentRepository
    {
        private readonly GitExecutable _gitExe;
        private readonly ITraceFactory _traceFactory;
        private readonly GitExeRepository _repository;
        
        public GitDeploymentRepository(string path, ITraceFactory traceFactory)
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

        public void Clean()
        {
            _repository.Clean();
        }

        public ChangeSet GetChangeSet(string id)
        {
            return _repository.GetChangeSet(id);
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
                string fullNewId = _repository.Resolve(branch);

                return new PushInfo
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
            _repository.Update();
        }

        public void Update(string id)
        {
            _repository.Update(id);
        }
    }
}
