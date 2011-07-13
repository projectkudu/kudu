using System;
using System.Web.Mvc;
using Kudu.Core.Editor;

namespace Kudu.Services.Documents {
    [JsonExceptionFilter]
    public class FilesController : Controller {
        private readonly IFileSystem _fileSystem;
        public FilesController(IFileSystem fileSystem) {
            _fileSystem = fileSystem;
        }

        [HttpGet]
        [ActionName("index")]
        public ActionResult GetFiles(string path) {
            if (String.IsNullOrEmpty(path)) {
                return Json(_fileSystem.GetFiles(), JsonRequestBehavior.AllowGet);
            }
            return Content(_fileSystem.ReadAllText(path));
        }

        [HttpPost]
        [ValidateInput(false)]
        public void Save(string path, string content) {
            _fileSystem.WriteAllText(path, content);
        }

        [HttpPost]
        public void Delete(string path) {
            _fileSystem.Delete(path);
        }
    }
}
