using System;
using System.ComponentModel;
using System.Json;
using System.ServiceModel;
using System.ServiceModel.Web;
using Kudu.Core;
using Kudu.Core.SourceControl;

namespace Kudu.Services.SourceControl
{
    [ServiceContract]
    public class CloneService
    {
        private readonly IEnvironment _environment;
        private readonly IClonableRepository _repository;

        public CloneService(IEnvironment environment, IClonableRepository repository)
        {
            _environment = environment;
            _repository = repository;
        }

        [Description("Creates a clone copy of the source repository.")]
        [WebInvoke(UriTemplate = "clone")]
        public void Clone(JsonObject input)
        {
            var type = (RepositoryType)Enum.Parse(typeof(RepositoryType), (string)input["type"]);
            _repository.CloneRepository(_environment.DeploymentRepositoryPath, type);
        }
    }
}
