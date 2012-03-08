using System;
using System.ComponentModel;
using System.Net.Http;
using System.ServiceModel;
using System.ServiceModel.Web;
using Kudu.Core.SourceControl;
using Kudu.Services.Infrastructure;

namespace Kudu.Services.SourceControl
{
    [ServiceContract]
    public class DeploymentSourceControlService
    {
        private readonly IRepositoryManager _repositoryManager;
        private readonly IServerConfiguration _serverConfiguration;

        public DeploymentSourceControlService(IRepositoryManager repositoryManager, IServerConfiguration serverConfiguration)
        {
            _repositoryManager = repositoryManager;
            _serverConfiguration = serverConfiguration;
        }

        [Description("Gets the repository information.")]
        [WebGet(UriTemplate = "info")]
        public RepositoryInfo GetRepositoryInfo(HttpRequestMessage request)
        {
            var baseUri = new Uri(request.RequestUri.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped));
            return new RepositoryInfo
            {
                Type = _repositoryManager.GetRepositoryType(),
                GitUrl = UriHelper.MakeRelative(baseUri, _serverConfiguration.GitServerRoot),
            };
        }

        [Description("Does a clean of the repository.")]
        [WebInvoke(UriTemplate = "clean", Method = "POST")]
        public void Clean()
        {
            _repositoryManager.Clean();
        }

    }
}
