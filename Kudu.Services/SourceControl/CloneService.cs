using System.ServiceModel;
using System.ServiceModel.Web;
using Kudu.Core.SourceControl;

namespace Kudu.Services.SourceControl
{
    [ServiceContract]
    public class CloneService
    {
        private readonly IRepositoryManager _repositoryManager;

        public CloneService(IRepositoryManager repositoryManager)
        {
            _repositoryManager = repositoryManager;
        }

        [WebInvoke]
        public void Clone(SimpleJson.JsonObject input)
        {
            _repositoryManager.CloneRepository((string)input["source"], (RepositoryType)(long)input["type"]);
        }
    }
}
