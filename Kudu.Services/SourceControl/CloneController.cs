using System.Web.Mvc;
using Kudu.Core.SourceControl;
using Kudu.Services.Infrastructure;

namespace Kudu.Services.SourceControl {
    public class CloneController : KuduController {
        private readonly IRepositoryManager _repositoryManager;

        public CloneController(IRepositoryManager repositoryManager) {
            _repositoryManager = repositoryManager;
        }
        
        [HttpPost]
        public void Clone(string source, RepositoryType type) {
            _repositoryManager.CloneRepository(source, type);
        }
    }
}