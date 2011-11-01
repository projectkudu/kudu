using System;
using System.Json;
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
        public void Clone(JsonObject input)
        {
            _repositoryManager.CloneRepository((string)input["source"], (RepositoryType)Enum.Parse(typeof(RepositoryType), (string)input["type"]));
        }
    }
}
