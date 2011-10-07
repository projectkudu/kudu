using System.Web.Mvc;
using Kudu.Core.SourceControl;
using Kudu.Core.SourceControl.Hg;
using Kudu.Services.Infrastructure;

namespace Kudu.Services.SourceControl {
    public class DeploymentScmController : KuduController {
        private readonly IRepositoryManager _repositoryManager;
        private readonly IHgServer _server;

        public DeploymentScmController(IRepositoryManager repositoryManager,
                                       IHgServer server) {
            _repositoryManager = repositoryManager;
            _server = server;
        }

        [HttpPost]
        public void Create(RepositoryType type) {
            _repositoryManager.CreateRepository(type);
        }

        [HttpPost]
        public void Delete() {
            // Stop the server (will no-op if nothing is running)
            _server.Stop();
            _repositoryManager.Delete();
        }

        [HttpGet]
        [ActionName("kind")]
        public RepositoryType GetRepositoryType() {
            return _repositoryManager.GetRepositoryType();
        }
    }
}
