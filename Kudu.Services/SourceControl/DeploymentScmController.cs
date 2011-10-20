using System.ServiceModel;
using System.ServiceModel.Web;
using Kudu.Core.SourceControl;
using Kudu.Core.SourceControl.Hg;

namespace Kudu.Services.SourceControl {
    [ServiceContract]
    public class DeploymentScmController {
        private readonly IRepositoryManager _repositoryManager;
        private readonly IHgServer _server;

        public DeploymentScmController(IRepositoryManager repositoryManager,
                                       IHgServer server) {
            _repositoryManager = repositoryManager;
            _server = server;
        }

        [WebInvoke]
        public void Create(SimpleJson.JsonObject input) {
            _repositoryManager.CreateRepository((RepositoryType)(long)input["type"]);
        }

        [WebInvoke]
        public void Delete() {
            // Stop the server (will no-op if nothing is running)
            _server.Stop();
            _repositoryManager.Delete();
        }

        [WebGet(UriTemplate = "kind")]
        public RepositoryType GetRepositoryType() {
            return _repositoryManager.GetRepositoryType();
        }
    }
}
