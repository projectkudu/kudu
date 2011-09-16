using System;
using System.Web.Mvc;
using Kudu.Core.Editor;
using Kudu.Services.Infrastructure;

namespace Kudu.Services.Documents {
    public class FilesController : KuduController {
        private readonly IEditorFileSystem _fileSystem;

        public FilesController(IEditorFileSystemFactory fileSystemFactory) {
            _fileSystem = fileSystemFactory.CreateEditorFileSystem();
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
