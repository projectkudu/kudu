using System.ComponentModel;
using System.Json;
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

        [Description("Gets the project.")]
        [WebGet(UriTemplate = "")]
        public Project GetProject()
        {
            return _projectSystem.GetProject();
        }

        [Description("Gets the specified file.")]
        [WebGet(UriTemplate = "?path={path}")]
        public HttpResponseMessage GetFile(string path)
        {
            var content = new StringContent(_projectSystem.ReadAllText(path), System.Text.Encoding.UTF8);
            var response = new HttpResponseMessage();
            response.Content = content;
            return response;
        }

        [Description("Saves the specified file.")]
        [WebInvoke(UriTemplate = "save")]
        public void Save(JsonObject input)
        {
            _projectSystem.WriteAllText((string)input["path"], (string)input["content"]);
        }

        [Description("Deletes the specified file.")]
        [WebInvoke(UriTemplate = "delete")]
        public void Delete(JsonObject input)
        {
            _projectSystem.Delete((string)input["path"]);
        }
    }
}
