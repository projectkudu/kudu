using System;
using Kudu.Contracts.Infrastructure;
using Kudu.Core.Tracing;

namespace Kudu.Core.SourceControl
{
    internal sealed class DeploymentRepository
    {
        private readonly IRepository _repository;

        public DeploymentRepository(IRepository repository)
        {
            _repository = repository;
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
    }
}
