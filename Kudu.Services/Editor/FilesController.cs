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

        /// <summary>
        /// Get the list of all files
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public Project GetFiles()
        {
            return _projectSystem.GetProject();
        }

        /// <summary>
        /// Get the content of a file
        /// </summary>
        /// <param name="path">path of the file relative to the root</param>
        /// <returns></returns>
        [HttpGet]
        public HttpResponseMessage GetFile(string path)
        {
            var content = new StringContent(_projectSystem.ReadAllText(path), Encoding.UTF8);
            var response = new HttpResponseMessage();
            response.Content = content;
            return response;
        }

        /// <summary>
        /// Set the content of a file, either creating or replacing it
        /// </summary>
        /// <param name="path">path of the file relative to the root</param>
        /// <param name="input">content of the file</param>
        [HttpPut]
        public void Save(string path, JObject input)
        {
            string content = input.Value<string>("content");
            _projectSystem.WriteAllText(path, content);
        }

        /// <summary>
        /// Delete a file
        /// </summary>
        /// <param name="path">path of the file relative to the root</param>
        [HttpDelete]
        public void Delete(string path)
        {
            _projectSystem.Delete(path);
        }
    }
}
