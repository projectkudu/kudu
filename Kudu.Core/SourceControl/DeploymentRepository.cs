using System;
using Kudu.Core.Tracing;

namespace Kudu.Core.SourceControl
{
    public class DeploymentRepository : IDeploymentRepository
    {
        private readonly ITraceFactory _traceFactory;
        private readonly IRepository _repository;

        public DeploymentRepository(IRepository repository, ITraceFactory traceFactory)
        {
            _repository = repository;
            _traceFactory = traceFactory;
        }

        public void Clean()
        {
            _repository.Clean();
        }

        public ChangeSet GetChangeSet(string id)
        {
            return _repository.GetChangeSet(id);
        }

        public void Update()
        {
            _repository.ClearLock();
            _repository.Update();
        }

        public void Update(string id)
        {
            _repository.ClearLock();
            _repository.Update(id);
        }

        public void UpdateSubmodules()
        {
            _repository.ClearLock();
            _repository.UpdateSubmodules();
        }

        public ReceiveInfo GetReceiveInfo()
        {
            return _repository.GetReceiveInfo();
        }
    }
}
