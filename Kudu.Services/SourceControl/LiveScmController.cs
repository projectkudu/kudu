using System;
using System.ComponentModel;
using System.Net.Http;
using System.ServiceModel.Web;
using System.Web.Http;
using Kudu.Core.SourceControl;
using Kudu.Services.Infrastructure;

namespace Kudu.Services.SourceControl
{
    public class LiveScmController : ApiController
    {
        private readonly IServerRepository _repository;
        private readonly IServerConfiguration _serverConfiguration;

        public LiveScmController(IServerRepository repository, IServerConfiguration serverConfiguration)
        {
            _repository = repository;
            _serverConfiguration = serverConfiguration;
        }

        [HttpGet]
        public RepositoryInfo GetRepositoryInfo(HttpRequestMessage request)
        {
            var baseUri = new Uri(request.RequestUri.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped));
            return new RepositoryInfo
            {
                Type = _repository.GetRepositoryType(),
                GitUrl = UriHelper.MakeRelative(baseUri, _serverConfiguration.GitServerRoot),
            };
        }

        [HttpPost]
        public void Clean()
        {
            _repository.Clean();
        }

    }
}
