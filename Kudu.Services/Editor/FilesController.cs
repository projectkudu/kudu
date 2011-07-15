using System;
using System.Web.Mvc;
using Kudu.Core.Editor;

namespace Kudu.Services.Documents {
    [FormattedExceptionFilter]
    public class FilesController : Controller {
        private readonly IFileSystem _fileSystem;

        public FilesController(IFileSystemFactory fileSystemFactory) {
            _fileSystem = fileSystemFactory.CreateFileSystem();
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
