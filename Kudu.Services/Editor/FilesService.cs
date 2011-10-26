using System.Net.Http;
using System.ServiceModel;
using System.ServiceModel.Web;
using Kudu.Core.Editor;

namespace Kudu.Services.Documents
{
    [ServiceContract]
    public class FilesService
    {
        private readonly IProjectSystem _projectSystem;

        public FilesService(IProjectSystem projectSystem)
        {
            _projectSystem = projectSystem;
        }

        [WebGet(UriTemplate = "")]
        public Project GetProject()
        {
            return _projectSystem.GetProject();
        }

        [WebGet(UriTemplate = "?path={path}")]
        public HttpResponseMessage GetFile(string path)
        {
            var content = new StringContent(_projectSystem.ReadAllText(path), System.Text.Encoding.UTF8);
            var response = new HttpResponseMessage();
            response.Content = content;
            return response;
        }

        [WebInvoke]
        public void Save(SimpleJson.JsonObject input)
        {
            _projectSystem.WriteAllText((string)input["path"], (string)input["content"]);
        }

        [WebInvoke]
        public void Delete(SimpleJson.JsonObject input)
        {
            _projectSystem.Delete((string)input["path"]);
        }
    }
}
