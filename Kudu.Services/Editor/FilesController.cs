using System.Collections.Generic;
using System.Net.Http;
using System.ServiceModel;
using System.ServiceModel.Web;
using Kudu.Core.Editor;

namespace Kudu.Services.Documents {
    [ServiceContract]
    public class FilesController {
        private readonly IEditorFileSystem _fileSystem;

        public FilesController(IEditorFileSystemFactory fileSystemFactory) {
            _fileSystem = fileSystemFactory.CreateEditorFileSystem();
        }

        [WebGet(UriTemplate = "")]
        public IEnumerable<string> GetFiles() {
            return _fileSystem.GetFiles();
        }

        [WebGet(UriTemplate = "?path={path}")]
        public HttpResponseMessage GetFile(string path) {
            var content = new StringContent(_fileSystem.ReadAllText(path), System.Text.Encoding.UTF8);
            var response = new HttpResponseMessage();
            response.Content = content;
            return response;
        }

        [WebInvoke]
        public void Save(SimpleJson.JsonObject input) {
            _fileSystem.WriteAllText((string)input["path"], (string)input["content"]);
        }

        [WebInvoke]
        public void Delete(SimpleJson.JsonObject input) {
            _fileSystem.Delete((string)input["path"]);
        }
    }
}
