using System.Net.Http;
using System.Text;
using System.Web.Http;
using Kudu.Core.Editor;
using Newtonsoft.Json.Linq;

namespace Kudu.Services.Documents
{
    public class FilesController : ApiController
    {
        private readonly IProjectSystem _projectSystem;

        public FilesController(IProjectSystem projectSystem)
        {
            _projectSystem = projectSystem;
        }

        [HttpGet]
        public Project GetFiles()
        {
            return _projectSystem.GetProject();
        }

        [HttpGet]
        public HttpResponseMessage GetFile(string path)
        {
            var content = new StringContent(_projectSystem.ReadAllText(path), Encoding.UTF8);
            var response = new HttpResponseMessage();
            response.Content = content;
            return response;
        }

        [HttpPut]
        public void Save(string path, JObject input)
        {
            string content = input.Value<string>("content");
            _projectSystem.WriteAllText(path, content);
        }

        [HttpDelete]
        public void Delete(string path)
        {
            _projectSystem.Delete(path);
        }
    }
}
